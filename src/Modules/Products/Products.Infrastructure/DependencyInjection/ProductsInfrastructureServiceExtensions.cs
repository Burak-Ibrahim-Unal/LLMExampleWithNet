using Microsoft.Extensions.DependencyInjection;
using Products.Domain.Repositories;
using Products.Infrastructure.Persistence;

namespace Products.Infrastructure.DependencyInjection;

public static class ProductsInfrastructureServiceExtensions
{
    public static IServiceCollection AddProductsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        return services;
    }
}
