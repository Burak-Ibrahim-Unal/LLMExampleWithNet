namespace Shared.Application.Abstractions;

/// <summary>
/// Uygulama açılışında veritabanı şemasını hazır hâle getiren adımın soyutlaması.
/// </summary>
/// <remarks>
/// <c>Program.cs</c> açılışta önce bu adımı, ardından tohumlamayı (<see cref="IDatabaseSeeder"/>) ve bilgi tabanı
/// ingestion'ını çalıştırır. Arayüzün <c>Shared.Application</c>'da durması, host'un EF Core ayrıntısını bilmeden
/// "şemayı hazırla" diyebilmesini sağlar; somut uygulama <c>Shared.Infrastructure</c>'daki <c>DbMigrator</c>'dır.
/// </remarks>
public interface IDatabaseMigrator
{
    /// <summary>
    /// Şemayı oluşturur veya günceller. Şema zaten eksiksizse hiçbir şey yapmamalıdır; böylece her açılışta güvenle
    /// çağrılabilir.
    /// </summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
