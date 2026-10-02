namespace Shared.Kernel.Abstractions;

/// <summary>
/// Kalıcı silme yerine "silindi" işaretiyle (soft delete) çalışan varlıklar için işaret arayüzü:
/// <see cref="DeletedAtUtc"/> doluysa varlık silinmiş sayılır.
/// </summary>
/// <remarks>
/// Projenin dayandığı CQRS modüler monolit şablonundan kalmadır; şablondaki örnek ürün modülü bu arayüzü bir global
/// sorgu filtresiyle (<c>DeletedAtUtc == null</c>) birlikte kullanıyordu. SupportAssistant'ta hiçbir varlık bu arayüzü
/// uygulamaz ve <c>AppDbContext</c>'te buna bağlı bir filtre yoktur; yani şu an hiçbir kod yolu tarafından kullanılmaz.
/// Knowledge modülünün verisi <c>knowledge-base/</c> klasöründen türetildiği için klasörden kaldırılan dokümanlar
/// veritabanından da kalıcı olarak silinir; geri alınabilir silmeye ihtiyaç yoktur.
/// </remarks>
public interface ISoftDeletable
{
    /// <summary>Varlığın silindiği an (UTC); <c>null</c> ise varlık etkindir.</summary>
    DateTime? DeletedAtUtc { get; set; }
}
