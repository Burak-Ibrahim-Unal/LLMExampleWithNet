namespace Shared.Application.Abstractions;

/// <summary>
/// Şema hazırlandıktan sonra başlangıç verisi (ör. referans kayıtları) yüklemek için açılış adımının soyutlaması.
/// </summary>
/// <remarks>
/// Şablondan gelir. SupportAssistant'ın tek başlangıç verisi bilgi tabanı dokümanlarıdır ve bunlar tohumlama ile değil,
/// açılıştaki ingestion ile (<c>knowledge-base/*.md</c> → veritabanı) yüklenir; bu yüzden tek uygulaması olan
/// <c>DbSeeder</c> boştur. Arayüz, açılış sırasında (şema → tohum → ingestion) referans verisi için ayrılmış yeri korur.
/// </remarks>
public interface IDatabaseSeeder
{
    /// <summary>
    /// Başlangıç verisini yükler. Her açılışta çalıştığı için tekrar çağrıldığında aynı sonucu vermeli, veriyi
    /// çoğaltmamalıdır.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
