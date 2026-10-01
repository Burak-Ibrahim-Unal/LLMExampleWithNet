using Products.Application.BusinessRules;
using Products.Domain.Repositories;
using Products.Infrastructure.Persistence;
using Products.Service;
using Products.Service.Abstractions;
using Shared.Application.Abstractions;

namespace ECommerce.API.Extensions;

public static class ModuleServiceExtensions
{
    public static IServiceCollection AddModuleServices(this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ProductBusinessRules>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ISubscriptionQuotaService, SubscriptionQuotaService>();

        return services;
    }
}
