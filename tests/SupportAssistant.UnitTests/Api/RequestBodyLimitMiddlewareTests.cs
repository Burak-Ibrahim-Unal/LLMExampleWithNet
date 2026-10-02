using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Shared.Application.Common;
using Shouldly;
using SupportAssistant.API.Security;

namespace SupportAssistant.UnitTests.Api;

/// <summary>
/// <see cref="RequestBodyLimitMiddleware"/>'ın <c>Content-Length</c> başlığı olmayan (chunked) istekler için izlediği
/// yolu sınar: sunucunun istek başına gövde sınırını indirmesi ve okuma sırasında aşılan sınırı zarflı bir 413'e
/// çevirmesi.
/// </summary>
/// <remarks>
/// Entegrasyon testlerindeki bellek içi <c>TestServer</c> istek başına gövde sınırı özelliğini
/// (<see cref="IHttpMaxRequestBodySizeFeature"/>) sunmaz; orada yalnızca <c>Content-Length</c> yolu sınanabilir. Bu yol
/// Kestrel'de çalışır; burada ara katman sahte bir <see cref="HttpContext"/> ile doğrudan çağrılır.
/// </remarks>
public sealed class RequestBodyLimitMiddlewareTests
{
    /// <summary>Testlerde kullanılan gövde sınırı (bayt).</summary>
    private const long Limit = 1024;

    /// <summary>
    /// <c>Content-Length</c> başlığı olmayan bir istekte sunucunun gövde sınırının yapılandırılan değere indirildiğini ve
    /// isteğin bir sonraki ara katmana iletildiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Chunked bir gövdenin boyutu önceden bilinemez; reddetme ancak okuma sırasında, Kestrel'in kendi sınırıyla yapılabilir.
    /// Sınır indirilmeseydi Kestrel'in 30 MB'lık varsayılanı geçerli kalırdı.
    /// </remarks>
    [Fact]
    public async Task Requests_without_content_length_get_the_lower_server_body_limit()
    {
        var bodySize = new BodySizeFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(bodySize);
        var forwarded = false;

        await Create(_ =>
        {
            forwarded = true;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        bodySize.MaxRequestBodySize.ShouldBe(Limit);
        forwarded.ShouldBeTrue();
    }

    /// <summary>
    /// Okuma sırasında sınır aşıldığında Kestrel'in fırlattığı 413'ün (<see cref="BadHttpRequestException"/>) yakalanıp
    /// diğer hatalarla aynı <c>ApiResult</c> zarfıyla, sınırı söyleyen Türkçe mesajla yazıldığını doğrular.
    /// </summary>
    /// <remarks>
    /// Yakalanmasaydı istemci zarfsız bir 413 alırdı; istemcinin hata işleme kodu ise her hatayı aynı zarfla bekler.
    /// </remarks>
    [Fact]
    public async Task A_body_that_exceeds_the_limit_while_being_read_gets_the_413_envelope()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Create(_ => throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge))
            .InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        json.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("message").GetString()
            .ShouldBe(string.Format(CultureInfo.InvariantCulture, Messages.Security.RequestTooLarge, Limit));
    }

    /// <summary>Test edilen ara katmanı verilen sonraki adım ve <see cref="Limit"/> sınırıyla kurar.</summary>
    private static RequestBodyLimitMiddleware Create(RequestDelegate next) =>
        new(next, Options.Create(new SecurityOptions { MaxRequestBodyBytes = Limit }));

    /// <summary>
    /// Kestrel'in istek başına gövde sınırı özelliğinin yazılabilir bir taklidi; ara katmanın atadığı değeri saklar.
    /// </summary>
    private sealed class BodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        /// <summary>Gövde okunmaya başlanmadığı için sınır hâlâ değiştirilebilir.</summary>
        public bool IsReadOnly => false;

        /// <summary>Ara katmanın atadığı sınır; atanmadıysa null.</summary>
        public long? MaxRequestBodySize { get; set; }
    }
}
