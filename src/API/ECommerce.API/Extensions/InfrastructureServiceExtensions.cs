using Shared.Application.Abstractions;
using Shared.Infrastructure.DependencyInjection;
using Shared.Infrastructure.Persistence;

namespace ECommerce.API.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSharedInfrastructure(configuration, typeof(Products.Infrastructure.Persistence.ProductConfiguration).Assembly);

        services.AddScoped<IDatabaseMigrator, DbMigrator>();
        services.AddScoped<IDatabaseSeeder, DbSeeder>();

        return services;
    }
}
