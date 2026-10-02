namespace Knowledge.Application.Abstractions;

/// <summary>
/// Metni anlamsal arama için vektöre çeviren port. Embedding ucu yapılandırılmamışsa devre dışıdır.
/// </summary>
/// <remarks>
/// Gerçek adaptör (<c>OpenAiCompatibleEmbedder</c>) OpenAI uyumlu herhangi bir <c>/v1/embeddings</c> ucunu kullanır
/// (varsayılan kurulumda bge-m3 sunan uzak bir llama.cpp sunucusu). <c>Embeddings:BaseUrl</c> boşsa Null Object olan
/// <c>DisabledTextEmbedder</c> devreye girer; böylece kodun her yerinde null kontrolü yapmadan BM25-yalnız moda düşülür.
/// Testler gecikmesi, hatası ve sorgu vektörü ayarlanabilen <c>FakeTextEmbedder</c> kullanır; hiçbir test gerçek bir
/// embedding sunucusuna ihtiyaç duymaz.
/// </remarks>
public interface ITextEmbedder
{
    /// <summary>Bir embedding ucu yapılandırılmışsa true; false iken ingest vektör üretmez ve indeks BM25 ile çalışır.</summary>
    bool IsEnabled { get; }

    /// <summary>Saklanan vektörlerin yanına kaydedilir: farklı modellerin vektörleri asla karşılaştırılmamalıdır.</summary>
    /// <remarks>
    /// Model değiştiğinde eski vektörler bu ad sayesinde fark edilir: ingest o bölümleri yeniden embed eder, indeks de
    /// farklı modellerin vektörleri karışıkken hibrit aramayı açmaz.
    /// </remarks>
    string ModelName { get; }

    /// <summary>Doküman bölümlerini embed eder; vektörler girdilerle aynı sayıda ve aynı sırada döner.</summary>
    /// <remarks>
    /// Sıra sözleşmenin parçasıdır: ingest her vektörü dizinine göre ilgili bölüme yazar. Doküman ve sorgu için ayrı
    /// metotlar vardır, çünkü bazı modeller (e5, EmbeddingGemma) ikisi için farklı görev önekleri ister.
    /// </remarks>
    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<DocumentEmbeddingInput> inputs, CancellationToken cancellationToken = default);

    /// <summary>Bir arama sorgusunu embed eder; model gerektiriyorsa sorgu öneki adaptörde eklenir.</summary>
    Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>Embed edilecek tek bir doküman bölümü.</summary>
/// <param name="Title">
/// Doküman başlığı; doküman şablonunda başlık isteyen modeller için (ör. EmbeddingGemma önekindeki <c>{title}</c> yer
/// tutucusu) ayrıca verilir.
/// </param>
/// <param name="Text">Embed edilecek metin (ingest, kısa bölümlerin de bağlamla bulunabilmesi için başlığı ve bölüm yolunu metnin başına ekler).</param>
public sealed record DocumentEmbeddingInput(string Title, string Text);
