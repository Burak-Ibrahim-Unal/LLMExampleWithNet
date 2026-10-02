namespace SupportAssistant.API.Security;

/// <summary>
/// Her yanıta tarayıcıya yönelik güvenlik başlıklarını ekler: <c>X-Content-Type-Options: nosniff</c>,
/// <c>X-Frame-Options: DENY</c> ve <c>Referrer-Policy: no-referrer</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>nosniff</c> tarayıcının JSON yanıtını başka bir içerik türü (ör. betik) sanmasını, <c>DENY</c> Scalar arayüzünün
/// başka bir sitede çerçeve içine alınmasını (clickjacking), <c>no-referrer</c> ise adreslerin üçüncü taraflara
/// sızmasını önler. API yalnızca JSON ve Scalar sayfası sunduğu için bu üçü yan etkisizdir. Sıkı bir
/// <c>Content-Security-Policy</c> bilerek eklenmedi: Scalar betiklerini bir CDN'den yükler ve sayfa bozulurdu.
/// </para>
/// <para>
/// Başlıklar isteğin başında değil yanıt başlamadan hemen önce (<c>OnStarting</c>) yazılır; böylece sonradan yanıtı
/// temizleyen ara katmanlar (ör. hata işleyicisi) onları silemez ve 413, 429, 404 gibi hata yanıtları da başlıkları taşır.
/// </para>
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Başlıkları yanıt başlarken eklenecek şekilde kaydeder ve isteği bir sonraki ara katmana iletir.
    /// </summary>
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
