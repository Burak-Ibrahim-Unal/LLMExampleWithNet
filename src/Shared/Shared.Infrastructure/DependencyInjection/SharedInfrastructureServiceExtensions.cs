using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence;

namespace Shared.Infrastructure.DependencyInjection;

/// <summary>
/// Ortak kalıcılık altyapısını (paylaşılan <c>AppDbContext</c> ve modüllerin EF yapılandırma kaydı) DI kapsayıcısına
/// ekleyen extension metotları.
/// </summary>
/// <remarks>
/// Host (API), hangi modül derlemelerinin EF yapılandırması sağladığını buraya parametre olarak verir; böylece
/// <c>Shared.Infrastructure</c> hiçbir modülü referans almadan tüm modüllerin tablolarını tek bir DbContext'te toplar.
/// </remarks>
public static class SharedInfrastructureServiceExtensions
{
    /// <summary>
    /// Yapılandırmada <c>ConnectionStrings:DefaultConnection</c> yoksa kullanılan SQLite bağlantı dizesi: çalışma
    /// dizinindeki <c>supportassistant.db</c> dosyası. Veritabanı türetilmiş veri olduğu için yerel bir dosya yeterlidir.
    /// </summary>
    private const string DefaultConnectionString = "Data Source=supportassistant.db";

    /// <summary>
    /// <c>EntityConfigurationAssemblyRegistry</c>'yi singleton, <c>AppDbContext</c>'i SQLite ile scoped olarak kaydeder.
    /// </summary>
    /// <remarks>
    /// Bağlantı dizesi kayıt anında değil, DbContext her oluşturulduğunda DI'daki <c>IConfiguration</c>'dan okunur. Böylece
    /// pipeline'a sonradan eklenen yapılandırma da dikkate alınır; örneğin entegrasyon testlerindeki
    /// <c>WebApplicationFactory</c>, her test sunucusuna kendi geçici SQLite dosyasını bu yolla verir. Bu nedenle
    /// <paramref name="configuration"/> parametresi gövdede artık kullanılmıyor; şablondaki sürüm bağlantı dizesini kayıt
    /// anında bu parametreden okuyordu.
    /// </remarks>
    /// <param name="services">Kayıtların ekleneceği servis koleksiyonu.</param>
    /// <param name="configuration">Host yapılandırması; bağlantı dizesi bilerek buradan değil, çözümleme anında okunur.</param>
    /// <param name="configurationAssemblies">
    /// <c>IEntityTypeConfiguration</c> sınıfları taranacak modül derlemeleri (şu an yalnızca <c>Knowledge.Infrastructure</c>).
    /// </param>
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] configurationAssemblies)
    {
        services.AddSingleton(new EntityConfigurationAssemblyRegistry(configurationAssemblies));

        // Bağlantı dizesi kayıt anında değil, context oluşturulurken çözülür; böylece pipeline'a sonradan eklenen
        // yapılandırma (ör. test host'larının verdiği geçici veritabanı) da dikkate alınır.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
                ?? DefaultConnectionString;

            options.UseSqlite(connectionString);
        });

        return services;
    }
}
