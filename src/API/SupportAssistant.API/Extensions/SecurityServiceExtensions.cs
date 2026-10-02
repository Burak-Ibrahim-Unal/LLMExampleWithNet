using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Shared.Application.Common;
using SupportAssistant.API.Security;

namespace SupportAssistant.API.Extensions;

/// <summary>
/// API genelindeki koruma katmanlarının DI kayıtları: koruma ayarları (yönetici anahtarı) ve soru ucunun IP başına hız
/// sınırı.
/// </summary>
public static class SecurityServiceExtensions
{
    /// <summary>
    /// <see cref="SecurityOptions"/> ile <see cref="RateLimitingOptions"/>'ı yapılandırmaya bağlar ve soru ucunun hız
    /// sınırı politikasını (<see cref="RateLimitingOptions.QuestionsPolicy"/>) kaydeder.
    /// </summary>
    /// <remarks>
    /// Politika istemci başına bir dakikalık sabit pencere kullanır; kuyruk yoktur, sınırı aşan istek beklemeden 429 alır.
    /// İstemci, <see cref="ClientPartitionKey"/> ile tanınır: IPv4 adresi ya da IPv6 adresinin /64 ağı. Sınır değeri
    /// kayıt sırasında değil ilk istekte okunur: seçenekler tembel çözülür, böylece entegrasyon testlerinin yapılandırma
    /// üzerine yazmaları da geçerli olur. Değer 0 ya da negatifse politika sınırsız bir bölüm döndürür.
    /// </remarks>
    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitingOptions.QuestionsPolicy, httpContext =>
            {
                var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var client = ClientPartitionKey.For(httpContext.Connection.RemoteIpAddress);

                if (settings.QuestionsPerMinute <= 0)
                {
                    return RateLimitPartition.GetNoLimiter(client);
                }

                return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.QuestionsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });

            options.OnRejected = WriteTooManyRequestsAsync;
        });

        return services;
    }

    /// <summary>
    /// Sınırı aşan isteğe 429 durum kodunu, sınırlayıcının bildirdiği bekleme süresini <c>Retry-After</c> başlığında ve
    /// diğer hatalarla aynı <see cref="ApiResult{T}"/> zarfını yazar.
    /// </summary>
    /// <remarks>
    /// Varsayılan ret yanıtı gövdesiz bir 503'tür; istemcinin hata işleme kodu zarfı ve 429'u bekler. Sabit pencereli
    /// sınırlayıcı bekleme süresi olarak pencerenin kalanını değil tamamını (60 sn) bildirir; başlık bu yüzden bir üst
    /// sınırdır: pencere daha erken açılabilir, ama bu süre beklenirse istek kesinlikle kabul edilir. Süre yukarı
    /// yuvarlanır: sıfır saniye demek, istemcinin hemen yeniden deneyip yine reddedilmesi olurdu.
    /// </remarks>
    private static async ValueTask WriteTooManyRequestsAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await response.WriteAsJsonAsync(
            ApiResult<object>.Fail(Messages.Security.TooManyRequests, StatusCodes.Status429TooManyRequests),
            cancellationToken);
    }
}
