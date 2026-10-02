using System.Globalization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Shared.Application.Common;

namespace SupportAssistant.API.Security;

/// <summary>
/// İstek gövdesini <see cref="SecurityOptions.MaxRequestBodyBytes"/> ile sınırlar; aşan istek uç noktaya ulaşmadan 413
/// ve <see cref="ApiResult{T}"/> zarfıyla reddedilir.
/// </summary>
/// <remarks>
/// <para>
/// Bir destek sorusu en fazla 500 karakterdir ve iş kuralı daha uzununu 400 ile reddeder, ama ancak gövde okunup JSON
/// ayrıştırıldıktan sonra. Bu ara katman büyük gövdeyi okumadan reddeder: Kestrel'in varsayılan sınırı 30 MB'tır ve
/// her soru isteği bu kadar veriyi belleğe almaya zorlayabilirdi.
/// </para>
/// <para>
/// İki yol vardır. <c>Content-Length</c> başlığı sınırı aşıyorsa istek hemen reddedilir. Başlık yoksa (chunked gövde)
/// sunucunun istek başına gövde sınırı (<see cref="IHttpMaxRequestBodySizeFeature"/>) aynı değere indirilir; sınır
/// okuma sırasında aşılırsa Kestrel'in fırlattığı 413 yakalanır ve yanıt henüz başlamadıysa aynı zarfla yazılır. Değer
/// 0 ya da negatifse sınır uygulanmaz.
/// </para>
/// </remarks>
public sealed class RequestBodyLimitMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
{
    /// <summary>
    /// Gövde sınırını uygular; sınır aşılmışsa 413 yazar, aşılmamışsa isteği bir sonraki ara katmana iletir.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var limit = options.Value.MaxRequestBodyBytes;

        if (limit <= 0)
        {
            await next(context);
            return;
        }

        if (context.Request.ContentLength > limit)
        {
            await WriteTooLargeAsync(context, limit);
            return;
        }

        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
        {
            bodySize.MaxRequestBodySize = limit;
        }

        try
        {
            await next(context);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge && !context.Response.HasStarted)
        {
            await WriteTooLargeAsync(context, limit);
        }
    }

    /// <summary>
    /// 413 durum kodunu ve sınırı bayt olarak söyleyen Türkçe mesajı <see cref="ApiResult{T}"/> zarfıyla yazar.
    /// </summary>
    private static Task WriteTooLargeAsync(HttpContext context, long limit)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        var message = string.Format(CultureInfo.InvariantCulture, Messages.Security.RequestTooLarge, limit);

        return context.Response.WriteAsJsonAsync(
            ApiResult<object>.Fail(message, StatusCodes.Status413PayloadTooLarge),
            context.RequestAborted);
    }
}
