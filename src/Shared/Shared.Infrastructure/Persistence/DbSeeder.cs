using Shared.Application.Abstractions;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// <see cref="IDatabaseSeeder"/> için boş (no-op) uygulama.
/// </summary>
/// <remarks>
/// Şablondan geldiği hâliyle boştur. SupportAssistant'ın tek başlangıç verisi bilgi tabanı dokümanlarıdır ve bunlar
/// tohumlama ile değil, açılışta ingestion ile (<c>IKnowledgeService.ReindexAsync</c>) yüklenir; böylece dosya →
/// veritabanı uzlaştırması hem açılışta hem <c>POST /v1/documents/reindex</c> çağrısında aynı kod yolundan geçer.
/// Sınıf, açılış sırasını (şema → tohum → ingestion) koruyarak gerçek referans verisi gerekirse eklenecek yeri hazır tutar.
/// </remarks>
public sealed class DbSeeder : IDatabaseSeeder
{
    /// <summary>Hiçbir şey yapmaz ve tamamlanmış bir görev döndürür.</summary>
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
