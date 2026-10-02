using Knowledge.Infrastructure.DependencyInjection;
using Shared.Application.Abstractions;
using Shared.Infrastructure.DependencyInjection;
using Shared.Infrastructure.Persistence;

namespace SupportAssistant.API.Extensions;

/// <summary>
/// Altyapı katmanının DI kayıtları: paylaşılan kalıcılık (SQLite <c>AppDbContext</c>), Knowledge adaptörleri ve açılış
/// adımları (migrator, seeder).
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Ortak altyapıyı Knowledge modülünün EF yapılandırmalarıyla birlikte, ardından Knowledge adaptörlerini (bilgi
    /// tabanı kaynağı, arama indeksi, embedding ve dil modeli istemcileri) ve açılışta kullanılan migrator/seeder'ı
    /// kaydeder.
    /// </summary>
    /// <remarks>
    /// Modül derlemelerini <c>AddSharedInfrastructure</c>'a veren yer host'tur: <c>Shared.Infrastructure</c> modülleri
    /// tanımaz, host ise hepsini tanır. <paramref name="configuration"/>, Knowledge seçeneklerinin (<c>KnowledgeBase</c>,
    /// <c>Embeddings</c>, <c>Retrieval</c>, <c>Llm</c>) bağlanması için iletilir; embedding ve dil modeli adaptörleri bu
    /// seçeneklere göre seçilir (adres boşsa BM25-only mod veya "yapılandırılmamış" üretici). Migrator scoped
    /// <c>AppDbContext</c>'i kullandığı için scoped kaydedilir; seeder da açılışta aynı DI scope'u içinde çözülür.
    /// </remarks>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSharedInfrastructure(configuration, Knowledge.Infrastructure.AssemblyReference.Assembly);
        services.AddKnowledgeInfrastructure(configuration);

        services.AddScoped<IDatabaseMigrator, DbMigrator>();
        services.AddScoped<IDatabaseSeeder, DbSeeder>();

        return services;
    }
}
