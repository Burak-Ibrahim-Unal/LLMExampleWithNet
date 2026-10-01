using FastEndpoints;
using FastEndpoints.OpenApi;

namespace SupportAssistant.API.Extensions;

public static class WebApiServiceExtensions
{
    public static IServiceCollection AddWebApiServices(this IServiceCollection services)
    {
        services
            .AddFastEndpoints()
            .OpenApiDocument(options =>
            {
                options.DocumentName = "v1";
                options.Title = "SupportAssistant API";
                options.Version = "v1";
                options.ShortSchemaNames = true;
            });

        services.AddMediatR(config => config.RegisterServicesFromAssembly(Knowledge.Application.AssemblyReference.Assembly));

        return services;
    }
}
