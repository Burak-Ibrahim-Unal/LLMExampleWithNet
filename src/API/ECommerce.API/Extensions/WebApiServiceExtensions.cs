using FastEndpoints;
using FastEndpoints.Swagger;
using Products.Application.Commands.CreateProduct;

namespace ECommerce.API.Extensions;

public static class WebApiServiceExtensions
{
    public static IServiceCollection AddWebApiServices(this IServiceCollection services)
    {
        services
            .AddFastEndpoints()
            .SwaggerDocument();

        services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<CreateProductCommand>());

        return services;
    }
}
