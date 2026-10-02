using Knowledge.Application.Abstractions;
using Knowledge.Infrastructure.Embeddings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Embeddings;

/// <summary>
/// <see cref="OpenAiCompatibleEmbedder"/> birim testleri. Gerçek bir llama.cpp/bge-m3 sunucusu yerine MEAI'nin
/// <c>IEmbeddingGenerator</c> arayüzünü uygulayan kaydedici bir sahte kullanılır; böylece sunucuya gidecek metinler
/// (önekler, partilere bölme) ve dönen vektörlere uygulanan birim uzunluğa ölçekleme ağ olmadan doğrulanır.
/// </summary>
public sealed class OpenAiCompatibleEmbedderTests
{
    /// <summary>
    /// HTTP embedding endpoint'i sistemin dış sınırıdır; bu sahte üretici oraya gönderilecek olanı kaydeder. Her
    /// <c>GenerateAsync</c> çağrısını (gerçekte bir HTTP isteği) ayrı bir parti olarak saklar ve her girdi için sabit
    /// <c>[3, 4]</c> vektörünü döndürür; bu vektörün uzunluğu 5 olduğundan birim uzunluğa ölçeklenmiş hâli kolayca
    /// doğrulanabilen <c>[0.6, 0.8]</c> olur.
    /// </summary>
    private sealed class RecordingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        /// <summary>Gönderilen partiler, gönderim sırasıyla; her iç liste tek bir isteğin metinleridir.</summary>
        public List<List<string>> Batches { get; } = [];

        /// <summary>
        /// Partiyi kaydeder ve girdi sayısı kadar <c>[3, 4]</c> vektörü döndürür. Vektör sayısının girdi sayısına eşit
        /// olması gerekir; aksi hâlde adaptörün "eksik vektör" denetimi istisna fırlatırdı.
        /// </summary>
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var batch = values.ToList();
            Batches.Add(batch);
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(batch.Select(_ => new Embedding<float>(new float[] { 3f, 4f }))));
        }

        /// <summary>Ek servis sunmaz; yalnızca <c>IEmbeddingGenerator</c> sözleşmesi gereği vardır.</summary>
        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        /// <summary>Serbest bırakılacak kaynak yoktur; arayüz sözleşmesi gereği boştur.</summary>
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Test edilen embedder'ı verilen sahte üretici ve seçeneklerle kurar; <c>Options.Create</c>, DI'daki
    /// <c>IOptions&lt;EmbeddingOptions&gt;</c> bağlamasının yerini tutar.
    /// </summary>
    private static OpenAiCompatibleEmbedder CreateEmbedder(RecordingGenerator generator, EmbeddingOptions options) =>
        new(generator, Options.Create(options));

    /// <summary>
    /// Doküman metinlerine <c>DocumentPrefix</c> önekinin, içindeki <c>{title}</c> yer tutucusu doküman başlığıyla
    /// değiştirilerek eklendiğini ve <c>BatchSize</c> = 2 iken üç metnin sırası korunarak iki isteğe (2 + 1) bölündüğünü
    /// doğrular.
    /// </summary>
    /// <remarks>
    /// Bazı embedding modelleri (EmbeddingGemma, e5) doküman tarafında "title: … | text: …" biçiminde bir görev öneki
    /// bekler; önek yanlış kurulursa vektör kalitesi sessizce düşer. Partileme, uzak ve yavaş embedding sunucusuna tek bir
    /// dev istek yerine sınırlı boyutta istekler gönderir. Sıranın korunması da kritiktir: ingest, dönen vektörleri girdi
    /// sırasıyla chunk'lara yazar; sıra kayarsa her chunk başka bir bölümün vektörünü taşır.
    /// </remarks>
    [Fact]
    public async Task Document_texts_get_the_document_prefix_with_their_title_and_are_sent_in_batches()
    {
        var generator = new RecordingGenerator();
        var embedder = CreateEmbedder(generator, new EmbeddingOptions { DocumentPrefix = "title: {title} | text: ", BatchSize = 2 });

        await embedder.EmbedDocumentsAsync(
            [new DocumentEmbeddingInput("İade", "bir"), new DocumentEmbeddingInput("İade", "iki"), new DocumentEmbeddingInput("Kargo", "üç")],
            TestContext.Current.CancellationToken);

        generator.Batches.Count.ShouldBe(2);
        generator.Batches[0].ShouldBe(["title: İade | text: bir", "title: İade | text: iki"]);
        generator.Batches[1].ShouldBe(["title: Kargo | text: üç"]);
    }

    /// <summary>
    /// Sorgu metnine <c>QueryPrefix</c> önekinin eklendiğini ve sorgunun tek istekte gönderildiğini doğrular. Asimetrik
    /// embedding modelleri (ör. EmbeddingGemma) sorgu ve doküman için farklı görev önekleri bekler; önek unutulursa model
    /// eğitildiği biçimde kullanılmamış olur ve benzerlik skorları sessizce kötüleşir. Varsayılan model bge-m3 önek
    /// gerektirmediği için bu ayar varsayılan olarak boştur.
    /// </summary>
    [Fact]
    public async Task Query_text_gets_the_query_prefix()
    {
        var generator = new RecordingGenerator();
        var embedder = CreateEmbedder(generator, new EmbeddingOptions { QueryPrefix = "task: search result | query: " });

        await embedder.EmbedQueryAsync("iade süresi", TestContext.Current.CancellationToken);

        generator.Batches.ShouldHaveSingleItem().ShouldBe(["task: search result | query: iade süresi"]);
    }

    /// <summary>
    /// Sunucudan dönen <c>[3, 4]</c> vektörünün birim uzunluğa (<c>[0.6, 0.8]</c>) ölçeklendiğini doğrular. OpenAI uyumlu
    /// sunucuların hepsi normalize vektör döndürmez; adaptör bunu garanti ettiğinde saklanan vektörler sağlayıcıdan
    /// bağımsız olarak aynı ölçekte olur ve kosinüs benzerliği iç çarpıma indirgenebilir.
    /// </summary>
    [Fact]
    public async Task Returned_vectors_are_scaled_to_unit_length()
    {
        var embedder = CreateEmbedder(new RecordingGenerator(), new EmbeddingOptions());

        var vector = await embedder.EmbedQueryAsync("iade", TestContext.Current.CancellationToken);

        vector.ShouldBe([0.6f, 0.8f], tolerance: 1e-6f);
    }
}
