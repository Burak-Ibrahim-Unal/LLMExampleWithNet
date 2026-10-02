namespace Knowledge.Infrastructure.Embeddings;

/// <summary>
/// Embedding ucunun ayarları; yapılandırmadaki <c>Embeddings</c> bölümüne bağlanır (ortam değişkenlerinde
/// <c>Embeddings__BaseUrl</c> biçiminde, Development ortamında <c>.env</c> dosyasından). Gerçek
/// <see cref="OpenAiCompatibleEmbedder"/> ile Null Object olan <see cref="DisabledTextEmbedder"/> arasındaki seçim ve
/// gerçek adaptörün davranışı (model, önekler, grup boyutu, zaman aşımı) kod değişmeden yalnızca yapılandırmayla
/// yönetilir.
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>
    /// Bağlanılan yapılandırma bölümünün adı. Ortam değişkenlerinde çift alt çizgi bölüm ayırıcıdır
    /// (<c>Embeddings__Model</c> → <c>Embeddings:Model</c>).
    /// </summary>
    public const string SectionName = "Embeddings";

    /// <summary>
    /// OpenAI-uyumlu embedding ucunun kök adresi (<c>/v1</c> ile biten). Değer <c>.env</c> dosyasından veya ortam
    /// değişkeninden gelir, koda yazılmaz. Boş bırakılırsa vektör araması kapanır: <see cref="DisabledTextEmbedder"/>
    /// devreye girer, arama yalnızca BM25 ile çalışır ve uygulama yine de açılır.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Embedding sağlayıcısının API anahtarı. Varsayılan <c>"local"</c> bir yer tutucudur: yerel sunucular (llama.cpp,
    /// LM Studio, Ollama) anahtarı yok sayar ama OpenAI istemcisi boş olmayan bir değer ister. Gerçek anahtarlar yalnızca
    /// ortam değişkeni veya <c>.env</c> üzerinden verilir; asla kaynak koda ya da <c>appsettings.json</c>'a yazılmaz.
    /// </summary>
    public string ApiKey { get; set; } = "local";

    /// <summary>
    /// Embedding modelinin adı; istekte gönderilir ve saklanan her vektörün yanına kaydedilir. Ad değişince ingest tüm
    /// bölümleri yeniden embed eder; indeks de başka bir modelin ürettiği vektörleri sorgu vektörüyle asla
    /// karşılaştırmaz. Varsayılan bge-m3: Türkçe dahil çok dilli, 1024 boyutlu, 8192 token bağlamlı ve önek istemeyen
    /// bir model.
    /// </summary>
    public string Model { get; set; } = "bge-m3";

    /// <summary>
    /// Sorgu metninin başına eklenen metin. Bazı modeller (EmbeddingGemma, e5) sorgu ve doküman için farklı görev
    /// önekleriyle eğitilmiştir ve önek verilmezse arama kalitesi düşer. bge-m3 önek istemediği için varsayılan boştur.
    /// </summary>
    public string QueryPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Doküman (bölüm) metinlerinin başına eklenen metin; içindeki <c>{title}</c> yer tutucusu doküman başlığıyla
    /// değiştirilir (EmbeddingGemma'nın <c>title: {title} | text: </c> biçimi gibi). bge-m3 için varsayılan boştur.
    /// </summary>
    public string DocumentPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Tek embedding isteğinde gönderilen en fazla metin sayısı. Tüm bölümleri tek istekte göndermek isteği büyütüp
    /// zaman aşımı riskini artırır, her bölümü ayrı göndermek ise uzak sunucuya gereksiz gidiş-dönüş demektir; 16 bu
    /// ikisi arasında bir dengedir. 1'den küçük değerler 1 kabul edilir.
    /// </summary>
    public int BatchSize { get; set; } = 16;

    /// <summary>
    /// Embedding isteği başına ağ zaman aşımı (saniye). Embedding üretmek sohbet yanıtı üretmekten çok daha kısa
    /// sürdüğü için LLM'in sınırından düşüktür. Sunucu yanıt vermezse sorgu embedding'i zaman aşımıyla başarısız olur
    /// ve o soru yalnızca BM25 ile aranır.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
