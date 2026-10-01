using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence;

namespace Shared.Infrastructure.DependencyInjection;

public static class SharedInfrastructureServiceExtensions
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] configurationAssemblies)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=ecommerce.db";

        services.AddSingleton(new EntityConfigurationAssemblyRegistry(configurationAssemblies));

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        return services;
    }
}
