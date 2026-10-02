namespace Shared.Kernel.Abstractions;

/// <summary>
/// <see cref="EntityBase"/> türevi varlıklar için generic repository sözleşmesi: okuma, ekleme, güncelleme, silme ve
/// bekleyen değişiklikleri kaydetme.
/// </summary>
/// <remarks>
/// <c>Shared.Kernel</c>'de, EF Core'dan bağımsız tanımlıdır. Domain'deki repository arayüzleri
/// (<c>IKnowledgeDocumentRepository</c>, <c>IQuestionLogRepository</c>) bu sözleşmeyi genişletir; böylece Application
/// katmanı veritabanı teknolojisini bilmeden kalıcılık isteyebilir (mimari testleri Application'ın EF Core'a bağımlı
/// olmadığını doğrular). EF Core karşılığı <c>EfRepository&lt;T&gt;</c>'dir. Şablondan gelen genel bir sözleşmedir:
/// Knowledge modülü şu an yalnızca <see cref="AddAsync"/>, <see cref="Remove"/> ve <see cref="SaveChangesAsync"/>
/// üyelerini kullanır; okumalar için chunk'ları da yükleyen modüle özgü sorgular tanımlanmıştır.
/// </remarks>
/// <typeparam name="T">Kalıcı varlık tipi.</typeparam>
public interface IRepository<T> where T : EntityBase
{
    /// <summary>
    /// Varlığı teknik Guid anahtarıyla getirir; yoksa <c>null</c> döner. Knowledge modülü dokümanları Guid ile değil front
    /// matter kimliğiyle (<c>GetBySourceIdWithChunksAsync</c>) aradığı için bu genel üye şu an kullanılmıyor.
    /// </summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tüm varlıkları listeler. Knowledge modülü chunk'ları da yükleyen <c>ListWithChunksAsync</c> sorgusunu kullandığı
    /// için bu genel üye şu an kullanılmıyor.
    /// </summary>
    Task<List<T>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Yeni varlığı eklenecek olarak işaretler; veritabanına yazma <see cref="SaveChangesAsync"/> ile olur. Ekleme ile
    /// kaydetmenin ayrı olması, bir iş akışındaki tüm değişikliklerin (ör. ingestion'da eklenen, güncellenen ve silinen
    /// dokümanlar) tek kayıtta yazılmasını sağlar.
    /// </summary>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Değişiklik takibi dışındaki (detached) bir varlığı güncellenecek olarak işaretler. Takip altındaki varlıklarda
    /// gerekmez; EF Core uygulamasında değişen alanlar otomatik algılanır. Knowledge modülü varlıkları takip altında
    /// yükleyip doğrudan değiştirdiği için bu üye şu an kullanılmıyor.
    /// </summary>
    void Update(T entity);

    /// <summary>
    /// Varlığı silinecek olarak işaretler; silme <see cref="SaveChangesAsync"/> ile gerçekleşir. Ingestion, klasörden
    /// kaldırılmış dokümanları bununla siler. Veritabanı <c>knowledge-base/</c> dosyalarından türetildiği için soft delete
    /// yerine kalıcı silme yeterlidir.
    /// </summary>
    void Remove(T entity);

    /// <summary>
    /// Bekleyen değişiklikleri veritabanına yazar ve etkilenen satır sayısını döndürür. Ekleme/silme çağrılarından ayrı
    /// olması, handler'ın bir iş biriminin sonunda tek bir kayıt yapmasına olanak verir.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
