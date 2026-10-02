using System.Security.Cryptography;
using System.Text;
using FastEndpoints;
using Microsoft.Extensions.Options;
using Shared.Application.Common;

namespace SupportAssistant.API.Security;

/// <summary>
/// Yönetici uçlarının (<c>POST /v1/documents/reindex</c>) ön işlemcisi: istekteki <c>X-Admin-Key</c> başlığını
/// yapılandırmadaki <see cref="SecurityOptions.AdminApiKey"/> ile karşılaştırır. Anahtar tanımlı değilse 403, başlık
/// eksik ya da yanlışsa 401 döner; her iki durumda yanıt <see cref="ApiResult{T}"/> zarfındadır ve uç işleyicisi hiç
/// çalışmaz.
/// </summary>
/// <remarks>
/// <para>
/// Karşılaştırma sabit zamanlıdır: iki değerin SHA-256 özetleri <see cref="CryptographicOperations.FixedTimeEquals"/>
/// ile karşılaştırılır. Düz dize karşılaştırması ilk farklı karakterde durur ve yanıt süresi anahtarı karakter karakter
/// tahmin etmeye yarayabilirdi; özet ise uzunluk farkını da gizler.
/// </para>
/// <para>
/// Eksik ve yanlış anahtar aynı yanıtı alır ve reddedilen her deneme istemci IP'siyle uyarı olarak loglanır; gönderilen
/// değer loglanmaz. 401 yanıtı, standardın istediği <c>WWW-Authenticate</c> başlığıyla anahtarın hangi başlıkta
/// beklendiğini söyler. Kimlik doğrulama altyapısı (ASP.NET Core authentication) bilerek kurulmadı: korunacak tek bir
/// operatör işlemi var ve diğer uçlar anonim kalmalı.
/// </para>
/// </remarks>
public sealed class AdminKeyPreProcessor : IPreProcessor<EmptyRequest>
{
    /// <summary>
    /// Anahtarı denetler; denetim başarısızsa yanıtı gönderir. FastEndpoints, ön işlemci bir yanıt başlattığında uç
    /// işleyicisini çalıştırmaz.
    /// </summary>
    public async Task PreProcessAsync(IPreProcessorContext<EmptyRequest> context, CancellationToken ct)
    {
        var httpContext = context.HttpContext;
        var configured = httpContext.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value.AdminApiKey;

        if (string.IsNullOrWhiteSpace(configured))
        {
            await httpContext.Response.SendAsync(
                ApiResult<object>.Fail(Messages.Security.ReindexDisabled, StatusCodes.Status403Forbidden),
                StatusCodes.Status403Forbidden,
                null,
                ct);
            return;
        }

        var provided = httpContext.Request.Headers[SecurityOptions.AdminKeyHeader].ToString();

        if (KeysMatch(provided, configured))
        {
            return;
        }

        httpContext.RequestServices.GetRequiredService<ILogger<AdminKeyPreProcessor>>().LogWarning(
            "Rejected an admin request to {Path} from {Client}: missing or invalid {Header} header.",
            httpContext.Request.Path,
            httpContext.Connection.RemoteIpAddress,
            SecurityOptions.AdminKeyHeader);

        httpContext.Response.Headers.WWWAuthenticate = $"ApiKey header=\"{SecurityOptions.AdminKeyHeader}\"";
        await httpContext.Response.SendAsync(
            ApiResult<object>.Fail(Messages.Security.AdminKeyRequired, StatusCodes.Status401Unauthorized),
            StatusCodes.Status401Unauthorized,
            null,
            ct);
    }

    /// <summary>
    /// İki anahtarı SHA-256 özetleri üzerinden sabit zamanda karşılaştırır; süre, değerlerin ne kadarının eşleştiğine ya
    /// da uzunluklarına göre değişmez.
    /// </summary>
    private static bool KeysMatch(string provided, string configured) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(configured)));
}
