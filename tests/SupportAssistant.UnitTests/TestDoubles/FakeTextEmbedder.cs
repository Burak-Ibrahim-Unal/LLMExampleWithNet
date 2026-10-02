using Knowledge.Application.Abstractions;

namespace SupportAssistant.UnitTests.TestDoubles;

/// <summary>
/// Uzak embedding sunucusunun yerine geçer (<see cref="ITextEmbedder"/> portunun sahte uygulaması); vektörler
/// deterministik ve yapılandırılabilirdir.
/// </summary>
/// <remarks>
/// Testlerin ağa çıkmadan hibrit arama, BM25'e düşüş, embedding modeli değişimi ve eşzamanlı ingestion senaryolarını
/// kurabilmesi için her davranış ayrı bir ayarla kontrol edilir: <see cref="QueryVector"/>, <see cref="Failure"/>,
/// <see cref="Delay"/> ve kurucu parametreleri.
/// </remarks>
/// <param name="modelName">
/// Kayıtlı vektörlerin yanına yazılan ve onlarla karşılaştırılan model adı; varsayılanı <see cref="DefaultModel"/>.
/// Farklı bir ad (ör. "new-model") "embedding modeli değişti" senaryosunu kurar: indeks eski modelin vektörlerini
/// kullanmaz ve BM25'e düşer.
/// </param>
/// <param name="enabled">
/// <c>false</c> verildiğinde, <c>Embeddings:BaseUrl</c> boşken kullanılan devre dışı embedder gibi davranır: ingestion
/// vektör istemez, indeks yalnızca BM25 ile çalışır. Yanıt hattı testleri bunu deterministik bir lexical indeks için
/// kullanır.
/// </param>
internal sealed class FakeTextEmbedder(string modelName = FakeTextEmbedder.DefaultModel, bool enabled = true) : ITextEmbedder
{
    /// <summary>
    /// Varsayılan model adı. Testler kayıtlı vektörleri bu adla işaretler (<c>SetEmbedding(..., DefaultModel)</c>); ad
    /// embedder'ınkiyle eşleştiği için <c>KnowledgeIndex</c> bu vektörleri kullanılabilir sayar.
    /// </summary>
    public const string DefaultModel = "fake-embedding";

    /// <summary>Kurucudaki <c>enabled</c> değeri; <c>false</c> ise ingestion embedding istemez ve indeks BM25 ile çalışır.</summary>
    public bool IsEnabled => enabled;

    /// <summary>Kurucudaki <c>modelName</c> değeri; kayıtlı vektörlerin model adıyla karşılaştırılır.</summary>
    public string ModelName => modelName;

    /// <summary>
    /// Sorgu metninden sorgu vektörünü üreten fonksiyon (varsayılan [1, 0]). Testler bunu kayıtlı bir vektörle aynı yöne
    /// ayarlayarak anlamsal eşleşme (kosinüs = 1) kurar ya da farklı boyutta bir vektör döndürerek "sunucu aynı ad altında
    /// başka bir model çalıştırıyor" senaryosunu, yani BM25'e düşüşü sınar. Yalnızca nesne oluşturulurken atanabilir
    /// (<c>init</c>).
    /// </summary>
    public Func<string, float[]> QueryVector { get; init; } = _ => [1f, 0f];

    /// <summary>
    /// Doluysa hem doküman hem sorgu embedding çağrıları bu istisnayla başarısız olur. Embedding sunucusuna
    /// ulaşılamadığında ingestion'ın BM25 moduna düşerek yine başarılı olduğunu ve sorgusu embed edilemeyen aramanın
    /// lexical moda geçtiğini sınamak için kullanılır.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// Doküman embedding'i için gönderilen metinlerin kaydı; embedding'e hangi metnin gittiğini (başlık ve bölüm yolu öneki
    /// dâhil) ya da kaç metin gönderildiğini incelemek isteyen testler içindir.
    /// </summary>
    public List<string> EmbeddedDocumentTexts { get; } = [];

    /// <summary>
    /// Embedding sunucusunun ağ gecikmesini taklit eder. <c>ConcurrentIngestionTests</c> bunu, bir istek embedding'i
    /// beklerken diğerlerinin aynı kayıtlı durumu okuyabileceği yarış penceresini açmak için kullanır.
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Önce <see cref="Delay"/> kadar bekler (iptal belirtecine uyarak), <see cref="Failure"/> doluysa onu fırlatır; aksi
    /// hâlde metinleri kaydedip her girdi için deterministik bir vektör döndürür: [metin uzunluğu, 1].
    /// </summary>
    /// <remarks>
    /// Vektörün değeri önemli değildir; önemli olan her chunk için aynı boyutta (2) bir vektör üretilmesidir, çünkü indeks
    /// vektörleri ancak hepsi aynı modelden ve aynı boyutta gelirse hibrit modda kullanır. Kayıt listesine kilit altında
    /// eklenir: eşzamanlılık testinde birden çok ingestion aynı fake'i paylaşır ve ingestion kapısı bozulursa çağrılar
    /// paralel çalışır; kilit, bu durumda <c>List&lt;T&gt;</c>'nin bozulup testin asıl iddiası yerine anlaşılmaz bir hata
    /// üretmesini önler.
    /// </remarks>
    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<DocumentEmbeddingInput> inputs, CancellationToken cancellationToken = default)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        lock (EmbeddedDocumentTexts)
        {
            EmbeddedDocumentTexts.AddRange(inputs.Select(input => input.Text));
        }

        return inputs.Select(input => new[] { input.Text.Length, 1f }).ToList();
    }

    /// <summary>
    /// <see cref="Failure"/> doluysa hata veren bir görev, değilse <see cref="QueryVector"/> sonucunu döndürür. Gecikme
    /// uygulanmaz; <see cref="Delay"/> yalnızca ingestion yarışını kurmak için gereklidir.
    /// </summary>
    public Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        return Failure is not null ? Task.FromException<float[]>(Failure) : Task.FromResult(QueryVector(query));
    }
}
