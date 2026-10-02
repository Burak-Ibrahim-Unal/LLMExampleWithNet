using Knowledge.Application.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Embeddings;

/// <summary>
/// Metinleri herhangi bir OpenAI-uyumlu <c>/v1/embeddings</c> ucu (llama.cpp sunucusu, LM Studio, Ollama, OpenAI,
/// Gemini) üzerinden Microsoft.Extensions.AI ile vektöre çevirir. Bu projede uç, uzak bir llama.cpp sunucusunda çalışan
/// bge-m3 modelidir; sağlayıcıyı değiştirmek yalnızca yapılandırma işidir. Dönen vektörler birim uzunluğa ölçeklenir:
/// sağlayıcı vektörleri normalize etse de etmese de saklanan vektörler aynı biçimde olur, kosinüs benzerliği nokta
/// çarpımına eşitlenir ve sağlayıcıdan bağımsız olarak karşılaştırılabilir kalır.
/// </summary>
/// <remarks>
/// HTTP ayrıntısı enjekte edilen <c>IEmbeddingGenerator</c> arkasında kalır; bu sayede birim testleri gerçek bir sunucu
/// yerine isteği kaydeden sahte bir üreteçle önek, gruplama ve normalizasyon davranışını doğrulayabilir.
/// </remarks>
public sealed class OpenAiCompatibleEmbedder(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    IOptions<EmbeddingOptions> options) : ITextEmbedder
{
    /// <summary>
    /// Her zaman <c>true</c>: bu adaptör yalnızca <c>Embeddings:BaseUrl</c> doluyken oluşturulur. Sunucunun o an
    /// erişilebilir olduğunu garanti etmez; erişim hataları çağrı anında istisna olarak gelir, ingest ve indeks de
    /// bunları BM25'e düşerek karşılar.
    /// </summary>
    public bool IsEnabled => true;

    /// <summary>
    /// Yapılandırılmış model adı (<see cref="EmbeddingOptions.Model"/>). Ingest bu adı saklanan her vektörün yanına
    /// yazar; ad değişince bölümler yeniden embed edilir ve farklı modellerin vektörleri asla karşılaştırılmaz.
    /// </summary>
    public string ModelName => options.Value.Model;

    /// <summary>
    /// Ingest sırasında bölüm metinlerini embed eder. Her metnin başına <see cref="EmbeddingOptions.DocumentPrefix"/>
    /// eklenir (<c>{title}</c> doküman başlığıyla değiştirilir), metinler <see cref="EmbeddingOptions.BatchSize"/>
    /// büyüklüğünde gruplar hâlinde sırayla gönderilir ve vektörler girdi sırasıyla döndürülür.
    /// </summary>
    /// <remarks>
    /// Sıra korunmalıdır: ingest işleyicisi i'nci vektörü i'nci bölüme yazar. Herhangi bir grup başarısız olursa istisna
    /// yukarı taşınır ve hiçbir vektör döndürülmez, yani yarım kalmış bir atama olmaz; ingest bunu yakalayıp BM25 moduna
    /// geçer ve eksik vektörleri bir sonraki reindex tamamlar. <c>Math.Max(1, …)</c>, yanlış yapılandırılmış 0 veya
    /// negatif bir grup boyutunun <c>Chunk</c> çağrısında istisnaya yol açmasını önler.
    /// </remarks>
    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<DocumentEmbeddingInput> inputs, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var texts = inputs
            .Select(input => settings.DocumentPrefix.Replace("{title}", input.Title, StringComparison.Ordinal) + input.Text)
            .ToList();

        var vectors = new List<float[]>(texts.Count);

        foreach (var batch in texts.Chunk(Math.Max(1, settings.BatchSize)))
        {
            vectors.AddRange(await EmbedAsync(batch, cancellationToken));
        }

        return vectors;
    }

    /// <summary>
    /// Soruyu embed eder; başına <see cref="EmbeddingOptions.QueryPrefix"/> eklenir. Asimetrik modeller sorgu ve doküman
    /// için farklı önek beklediğinden sorgu yolu doküman yolundan ayrıdır. Her soruda bir kez
    /// <c>KnowledgeIndex.PrepareAsync</c> içinden çağrılır; oradaki hata yakalama sayesinde bir başarısızlık yalnızca o
    /// sorunun BM25 ile aranmasına yol açar.
    /// </summary>
    public async Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        var vectors = await EmbedAsync([options.Value.QueryPrefix + query], cancellationToken);
        return vectors[0];
    }

    /// <summary>
    /// Tek bir grup metni üretece gönderir, dönen vektör sayısını doğrular ve her vektörü birim uzunluğa çevirir.
    /// </summary>
    /// <remarks>
    /// Sayı uyuşmazlığında sessizce devam etmek yerine istisna fırlatılır: aksi hâlde vektörler bölümlere göre kayar,
    /// bölümler başka metinlerin vektörleriyle saklanır ve fark edilmesi çok zor, yanlış arama sonuçları ortaya çıkardı.
    /// </remarks>
    private async Task<IReadOnlyList<float[]>> EmbedAsync(string[] texts, CancellationToken cancellationToken)
    {
        var embeddings = await generator.GenerateAsync(texts, cancellationToken: cancellationToken);

        if (embeddings.Count != texts.Length)
        {
            throw new InvalidOperationException($"The embedding endpoint returned {embeddings.Count} vectors for {texts.Length} inputs.");
        }

        return embeddings.Select(embedding => ToUnitLength(embedding.Vector.Span)).ToList();
    }

    /// <summary>
    /// Vektörü L2 normuna bölerek birim uzunluğa ölçekler ve yeni bir dizi döndürür (üretecin belleği kopyalanır,
    /// değiştirilmez). Norm, yüksek boyutlu vektörlerde (bge-m3 için 1024) biriken yuvarlama hatasını azaltmak için
    /// <c>double</c> ile hesaplanır. Sıfır vektör olduğu gibi döner: sıfıra bölme <c>NaN</c> üretir ve <c>NaN</c> tüm
    /// benzerlik hesaplarını ve sıralamayı bozardı.
    /// </summary>
    private static float[] ToUnitLength(ReadOnlySpan<float> vector)
    {
        var result = vector.ToArray();
        var norm = Math.Sqrt(result.Sum(value => (double)value * value));

        if (norm == 0)
        {
            return result;
        }

        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (float)(result[i] / norm);
        }

        return result;
    }
}
