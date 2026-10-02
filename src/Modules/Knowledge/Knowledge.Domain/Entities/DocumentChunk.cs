using Shared.Kernel.Abstractions;

namespace Knowledge.Domain.Entities;

/// <summary>
/// Bir dokümanın aranabilir tek bölümü (chunk); yanıtların atıf yaptığı birimdir. Markdown'daki her <c>##</c>/<c>###</c>
/// başlığı bir chunk olur; uzun bölümler paragraf sınırlarından bölünür.
/// </summary>
/// <remarks>
/// Ödevin "her yanıt kullanılan dokümanı ve ilgili bölümü göstermeli" şartı bu varlık üzerinden karşılanır: arama, dil
/// modeline verilen bağlam (<c>C1..Cn</c>) ve atıf doğrulaması hep chunk düzeyinde çalışır. Chunk'lar
/// <see cref="KnowledgeDocument"/> aggregate'inin parçasıdır ve yalnızca onun üzerinden oluşturulur.
/// </remarks>
public sealed class DocumentChunk : EntityBase
{
    /// <summary>
    /// Yalnızca EF Core'un veritabanı satırından nesne oluşturması (materialization) için. Private tutulması, uygulama
    /// kodunun alanları boş, geçersiz bir chunk oluşturmasını engeller; EF ise private constructor'ı kullanabilir.
    /// </summary>
    private DocumentChunk()
    {
    }

    /// <summary>
    /// Yeni bir chunk oluşturur. <c>internal</c> olduğundan domain derlemesi dışından çağrılamaz; chunk'lar yalnızca
    /// <see cref="KnowledgeDocument.AddChunk"/> ile, doğru doküman kimliği ve sıra numarasıyla oluşturulur.
    /// </summary>
    internal DocumentChunk(Guid documentId, int order, string sectionPath, string content)
    {
        DocumentId = documentId;
        Order = order;
        SectionPath = sectionPath;
        Content = content;
    }

    /// <summary>
    /// Sahip dokümanın teknik Guid anahtarı (yabancı anahtar). Chunk'tan dokümana bir navigation özelliği yoktur; ilişki
    /// doküman tarafındaki koleksiyondan yönetilir.
    /// </summary>
    public Guid DocumentId { get; private set; }

    /// <summary>
    /// Chunk'ın doküman içindeki sıfır tabanlı sırası. Bölümlerin dosyadaki sırayla gösterilmesi
    /// (<c>GET /v1/documents/{id}</c>) ve arama indeksinin kararlı bir sırayla kurulması için kullanılır.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// Doküman içindeki başlık yolu, ör. "2. Destek Seviyeleri &gt; 2.2 Seviye 2 (L2)". Yanıt kaynaklarında bölüm adı
    /// olarak gösterilir; ayrıca BM25 metnine ve embedding metnine eklenerek kısa bölümlere bağlam kazandırır.
    /// </summary>
    public string SectionPath { get; private set; } = string.Empty;

    /// <summary>Bölümün metni: aranan, dil modeline verilen ve alıntıların içinde arandığı içerik.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Bölümün embedding vektörü; henüz hesaplanmadıysa veya embedding servisi kapalıysa <c>null</c>. SQLite'ta BLOB
    /// olarak saklanır (<c>float[]</c> ↔ <c>byte[]</c> dönüşümü EF yapılandırmasındadır). Saklanması, değişmeyen
    /// dokümanların her açılışta uzak embedding sunucusuna yeniden gönderilmesini önler.
    /// </summary>
    public float[]? Embedding { get; private set; }

    /// <summary>
    /// <see cref="Embedding"/> vektörünü üreten modelin adı; başka bir modelin vektörleri karşılaştırılabilir değildir.
    /// Yapılandırılan model değişirse ingestion bu alana bakarak vektörleri yeniden hesaplar; indeks de farklı modelden
    /// gelen vektörlerle dense aramayı açmaz, BM25'e düşer.
    /// </summary>
    public string? EmbeddingModel { get; private set; }

    /// <summary>
    /// Vektörü ve onu üreten modelin adını birlikte atar. İkisinin tek metotta atanması, hangi modelden geldiği bilinmeyen
    /// bir vektörün saklanmasını engeller; ingestion yeniden embedding gerekip gerekmediğine bu bilgiyle karar verir.
    /// </summary>
    public void SetEmbedding(float[] embedding, string model)
    {
        Embedding = embedding;
        EmbeddingModel = model;
    }
}
