using Shared.Kernel.Abstractions;

namespace Knowledge.Domain.Entities;

/// <summary>
/// Bilgi tabanındaki bir dokümanın tek bir sürümü (aggregate root). Aynı prosedürün sürümleri ortak bir
/// <see cref="DocumentKey"/> (ör. "iade-politikasi") paylaşır; <see cref="Version"/>, <see cref="EffectiveDate"/> ve
/// <see cref="Status"/> ile birbirinden ayrılır.
/// </summary>
/// <remarks>
/// Her Markdown dosyası bir sürümdür; ör. <c>iade-politikasi-v1</c> ve <c>iade-politikasi-v2</c> ayrı nesnelerdir.
/// Sürümleri ayrı kayıtlar olarak tutmak, eski sürümün de aranabilmesini ve <c>VersionResolver</c>'ın hangisinin
/// yürürlükte olduğuna kodda, açıklanabilir biçimde karar vermesini sağlar. Bölümler (<see cref="Chunks"/>) bu
/// aggregate'e aittir ve yalnızca onun metotlarıyla değiştirilir. Tüm alanlar private set'tir: meta veriler yalnızca
/// ingestion'ın dosya değiştiğinde çağırdığı <see cref="Revise"/> ile, hep birlikte güncellenir; böylece kayıt dosyayla
/// yarı güncellenmiş, tutarsız bir duruma düşmez.
/// </remarks>
public sealed class KnowledgeDocument : EntityBase
{
    /// <summary>
    /// <see cref="Chunks"/> koleksiyonunun arka alanı. EF yapılandırması navigation'ı doğrudan bu alana bağlar; dışarıya
    /// yalnızca salt okunur görünüm verilir.
    /// </summary>
    private readonly List<DocumentChunk> _chunks = [];

    /// <summary>
    /// Yalnızca EF Core'un veritabanı satırından nesne oluşturması (materialization) için. Private tutulması, uygulama
    /// kodunun zorunlu alanları boş bir doküman oluşturmasını engeller; EF ise private constructor'ı kullanabilir.
    /// </summary>
    private KnowledgeDocument()
    {
    }

    /// <summary>
    /// Bilgi tabanında ilk kez görülen bir dosya için yeni sürüm kaydı oluşturur. Değişmez kimlik
    /// (<paramref name="sourceId"/>) burada atanır; geri kalan meta veriler <see cref="Revise"/> üzerinden atanır. Böylece
    /// "oluştur" ve "güncelle" aynı atama kodunu paylaşır ve ikisi birbirinden sapamaz.
    /// </summary>
    public KnowledgeDocument(
        string sourceId,
        string documentKey,
        string title,
        string version,
        DateOnly effectiveDate,
        DocumentStatus status,
        DocumentCategory category,
        string? supersedes,
        string contentHash)
    {
        SourceId = sourceId;
        Revise(documentKey, title, version, effectiveDate, status, category, supersedes, contentHash);
    }

    /// <summary>
    /// Dosyanın front matter'ındaki değişmez kimlik, ör. "iade-politikasi-v2". API'de doküman kimliği olarak gösterilir
    /// (<c>GET /v1/documents/{id}</c>, yanıt kaynaklarındaki <c>documentId</c>) ve ingestion'da kayıtların dosyalarla
    /// eşleştirildiği anahtardır; veritabanında benzersiz indekslidir. Teknik Guid anahtarı (<c>Id</c>) ise yalnızca
    /// veritabanı içi ilişkiler içindir.
    /// </summary>
    public string SourceId { get; private set; } = string.Empty;

    /// <summary>
    /// Sürüm ailesinin anahtarı, ör. "iade-politikasi". Aynı anahtarı paylaşan dokümanlar aynı prosedürün sürümleridir;
    /// <c>VersionResolver</c> arama adaylarını bu anahtara göre gruplar ve her aileden yalnızca yürürlükteki sürümü bırakır.
    /// </summary>
    public string DocumentKey { get; private set; } = string.Empty;

    /// <summary>
    /// Doküman başlığı. Yanıt kaynaklarında gösterilir; ayrıca aranan metne ve embedding metnine eklenerek bölümlere
    /// bağlam kazandırır.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Sürüm etiketi, ör. "2.0". Kullanıcıya gösterilir; yürürlük tarihi aynı olan iki sürüm arasında seçimde ikincil
    /// ölçüt olarak sayısal karşılaştırılır.
    /// </summary>
    public string Version { get; private set; } = string.Empty;

    /// <summary>
    /// Sürümün yürürlüğe girdiği tarih. Bir aileden, bugün veya daha önce yürürlüğe girmiş en yeni sürüm seçilir; tarihi
    /// henüz gelmemiş sürüm kullanılmaz. "Bugün", test edilebilirlik için enjekte edilen <c>TimeProvider</c>'dan gelir.
    /// </summary>
    public DateOnly EffectiveDate { get; private set; }

    /// <summary>
    /// Sürümün durumu; <c>Superseded</c> işaretli sürüm, yürürlük tarihine bakılmaksızın hiçbir yanıtta kaynak olmaz.
    /// </summary>
    public DocumentStatus Status { get; private set; }

    /// <summary>Doküman türü; farklı dokümanlar çeliştiğinde yetki sırasını (<c>SourcePrecedence</c>) belirler.</summary>
    public DocumentCategory Category { get; private set; }

    /// <summary>
    /// Bu sürümün yerini aldığı sürümün kimliği (front matter <c>supersedes</c>), ör. "iade-politikasi-v1"; yoksa
    /// <c>null</c>. Bilgi amaçlıdır ve doküman uç noktalarında gösterilir: sürüm seçimi bu alana değil,
    /// <see cref="Status"/> ve <see cref="EffectiveDate"/> değerlerine dayanır.
    /// </summary>
    public string? Supersedes { get; private set; }

    /// <summary>
    /// Kaynak dosyanın içerik özeti (satır sonları normalize edilmiş dosya metninin SHA-256'sı; front matter dahil).
    /// Özet değişmediyse saklanan chunk'lar ve embedding'ler hâlâ geçerlidir; ingestion bu sayede değişmeyen dokümanları
    /// yeniden bölmez ve uzak embedding sunucusuna yeniden göndermez.
    /// </summary>
    public string ContentHash { get; private set; } = string.Empty;

    /// <summary>
    /// Dokümanın bölümleri, dosyadaki sırayla. Dışarıya salt okunur görünüm verilir; değişiklik yalnızca
    /// <see cref="AddChunk"/> ve <see cref="ClearChunks"/> ile yapılır.
    /// </summary>
    public IReadOnlyList<DocumentChunk> Chunks => _chunks;

    /// <summary>
    /// Dosyadan okunan meta verileri ve yeni içerik özetini dokümana yazar. Ingestion, saklanan özet dosyanınkinden
    /// farklıysa (yalnızca meta veri değişmiş olsa bile) bunu çağırır, ardından <see cref="ClearChunks"/> ve
    /// <see cref="AddChunk"/> ile bölümleri yeniden kurar.
    /// </summary>
    /// <remarks>
    /// Mevcut kaydı silip yeniden eklemek yerine yerinde güncellemek, teknik kimliği ve <c>CreatedAtUtc</c> değerini korur.
    /// <see cref="SourceId"/> parametre olarak alınmaz: ingestion kayıtları bu kimlikle eşleştirdiği için kimliği değişen
    /// bir dosya yeni kayıt olarak eklenir, eskisi silinir. Constructor da bu metodu kullandığından alan ataması tek
    /// yerdedir.
    /// </remarks>
    public void Revise(
        string documentKey,
        string title,
        string version,
        DateOnly effectiveDate,
        DocumentStatus status,
        DocumentCategory category,
        string? supersedes,
        string contentHash)
    {
        DocumentKey = documentKey;
        Title = title;
        Version = version;
        EffectiveDate = effectiveDate;
        Status = status;
        Category = category;
        Supersedes = supersedes;
        ContentHash = contentHash;
    }

    /// <summary>
    /// Dokümana yeni bir bölüm ekler ve oluşturulan chunk'ı döndürür. Sıra numarası mevcut bölüm sayısından, doküman
    /// bağlantısı bu dokümanın <c>Id</c>'sinden otomatik alınır; böylece çağıran tutarsız bir sıra veya yanlış bir doküman
    /// kimliği veremez.
    /// </summary>
    /// <remarks>
    /// Dönen chunk ile hemen işlem yapılabilir (ör. testlerde <c>AddChunk(...).SetEmbedding(...)</c>); ingestion ise
    /// vektörleri sonradan, eksik olan tüm chunk'lar için toplu olarak atar.
    /// </remarks>
    public DocumentChunk AddChunk(string sectionPath, string content)
    {
        var chunk = new DocumentChunk(Id, _chunks.Count, sectionPath, content);
        _chunks.Add(chunk);
        return chunk;
    }

    /// <summary>
    /// Dokümanın tüm bölümlerini kaldırır. Değişen bir dosya yeniden bölünmeden önce çağrılır: başlıklar eklenip
    /// silinebildiği için eski bölümler tek tek eşleştirilmez, hepsi yeniden kurulur.
    /// </summary>
    /// <remarks>
    /// Koleksiyondan çıkarılan chunk'lar, ilişki zorunlu olduğu için (yetim kalan bağımlı kayıt) EF Core tarafından kayıt
    /// sırasında veritabanından silinir; yeni chunk'lar yeni kimliklerle eklenir ve embedding'leri yeniden hesaplanır.
    /// </remarks>
    public void ClearChunks()
    {
        _chunks.Clear();
    }
}
