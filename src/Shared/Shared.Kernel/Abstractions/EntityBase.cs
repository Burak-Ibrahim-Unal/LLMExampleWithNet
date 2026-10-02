namespace Shared.Kernel.Abstractions;

/// <summary>
/// Kalıcı tüm varlıkların ortak temel sınıfı: teknik kimliği (<see cref="Id"/>) ve denetim amaçlı zaman damgalarını
/// (<see cref="CreatedAtUtc"/>, <see cref="UpdatedAtUtc"/>) tek yerde tanımlar.
/// </summary>
/// <remarks>
/// Projenin dayandığı CQRS modüler monolit şablonundan gelir. <c>Shared.Kernel</c> içinde durduğu için EF Core'a ve
/// <c>Microsoft.Extensions</c> paketlerine bağımlı değildir (mimari testleri bunu doğrular); böylece domain varlıkları saf
/// C# olarak kalır. Generic <c>IRepository&lt;T&gt;</c> / <c>EfRepository&lt;T&gt;</c> bu tipi kısıt olarak kullanır;
/// <c>AppDbContext</c> değişen varlıkları bu tip üzerinden bulup güncelleme damgasını basar.
/// </remarks>
public abstract class EntityBase
{
    /// <summary>
    /// Varlığın teknik birincil anahtarı. Değer veritabanında değil, nesne oluşturulurken domain'de atanır; bu yüzden EF
    /// yapılandırmaları <c>ValueGeneratedNever</c> kullanır. Bu ayar olmadan EF Core, anahtarı zaten dolu yeni bir chunk'ı
    /// mevcut satır sanıp INSERT yerine UPDATE üretiyor ve kayıt <c>DbUpdateConcurrencyException</c> ile düşüyordu.
    /// </summary>
    /// <remarks>
    /// API dokümanları bu Guid ile değil, front matter'daki okunabilir kimlikle (<c>KnowledgeDocument.SourceId</c>) anar;
    /// Guid yalnızca veritabanı içi ilişkiler (ör. chunk → doküman) içindir.
    /// </remarks>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Nesnenin oluşturulduğu an (UTC). Yeni nesnede alan başlatıcısı doldurur; veritabanından okunan varlıkta EF Core
    /// saklanan değeri geri yazar. Yalnızca denetim ve sıralama içindir (ör. <c>question_logs</c> tablosunda bu sütuna
    /// indeks vardır); "bugün" gibi iş kararları buradan değil, enjekte edilen <c>TimeProvider</c>'dan beslenir.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Son güncelleme anı (UTC); hiç güncellenmemiş varlıkta <c>null</c>. Elle atanmaz: <c>AppDbContext</c> kayıt
    /// sırasında <c>Modified</c> durumdaki her varlık için <see cref="MarkUpdated"/> çağırır.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>
    /// <see cref="UpdatedAtUtc"/> değerini verilen ana, verilmezse şimdiki UTC zamana ayarlar.
    /// </summary>
    /// <remarks>
    /// Setter private olduğundan damganın tek giriş noktası bu metottur. Tek çağıranı <c>AppDbContext</c>'tir ve
    /// parametresiz çağırır; isteğe bağlı <paramref name="utcNow"/> zamanın dışarıdan verilebilmesine (ör. deterministik
    /// bir test) olanak tanır. Damgalamanın DbContext'te merkezi yapılması, her domain metodunun bunu ayrıca hatırlama
    /// zorunluluğunu ortadan kaldırır.
    /// </remarks>
    /// <param name="utcNow">Kullanılacak UTC zaman; <c>null</c> ise <c>DateTime.UtcNow</c>.</param>
    public void MarkUpdated(DateTime? utcNow = null)
    {
        UpdatedAtUtc = utcNow ?? DateTime.UtcNow;
    }
}
