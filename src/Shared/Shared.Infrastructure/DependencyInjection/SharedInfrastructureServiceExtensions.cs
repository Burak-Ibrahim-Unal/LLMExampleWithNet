using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence;

namespace Shared.Infrastructure.DependencyInjection;

public static class SharedInfrastructureServiceExtensions
{
    private const string DefaultConnectionString = "Data Source=supportassistant.db";

    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] configurationAssemblies)
    {
        services.AddSingleton(new EntityConfigurationAssemblyRegistry(configurationAssemblies));

        // The connection string is resolved when the context is built (not at registration time),
        // so configuration added later in the pipeline — e.g. by test hosts — is honoured.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
                ?? DefaultConnectionString;

            options.UseSqlite(connectionString);
        });

        return services;
    }
}
