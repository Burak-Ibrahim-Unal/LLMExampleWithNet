using FastEndpoints;
using FastEndpoints.OpenApi;

namespace SupportAssistant.API.Extensions;

/// <summary>
/// Web katmanının DI kayıtları: FastEndpoints, OpenAPI belgesi ve MediatR handler'ları.
/// </summary>
public static class WebApiServiceExtensions
{
    /// <summary>
    /// FastEndpoints'i (uç nokta sınıfları otomatik keşfedilir), <c>v1</c> adlı OpenAPI belgesini ve
    /// <c>Knowledge.Application</c> derlemesindeki MediatR handler'larını kaydeder.
    /// </summary>
    /// <remarks>
    /// <c>ShortSchemaNames</c>, şemaların tam ad alanı yerine kısa tip adlarıyla (ör. <c>AnswerDto</c>) görünmesini sağlar;
    /// belge ve Scalar arayüzü okunur kalır. MediatR yalnızca Application derlemesini tarar, çünkü tüm komut/sorgu
    /// handler'ları oradadır. MediatR 14'ün lisans anahtarı kodda verilmez: kütüphane <c>MEDIATR_LICENSE_KEY</c> ortam
    /// değişkenini kendisi okur; anahtar yoksa açılışta yalnızca bir uyarı loglar.
    /// </remarks>
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
