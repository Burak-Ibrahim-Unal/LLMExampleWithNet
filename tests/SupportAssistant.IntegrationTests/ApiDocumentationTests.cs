using System.Net;
using System.Text.Json;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

/// <summary>
/// API'nin kendini belgeleyen yüzeyini, yani OpenAPI dokümanını ve Scalar arayüzünü doğrular. Değerlendirici ve
/// istemciler API'yi bu iki adres üzerinden keşfeder; bunların kırılması iş mantığında değil kullanılabilirlikte bir
/// regresyondur ve başka hiçbir test onu yakalamaz.
/// </summary>
public sealed class ApiDocumentationTests(SupportAssistantApiFactory factory) : IClassFixture<SupportAssistantApiFactory>
{
    /// <summary>
    /// <c>/openapi/v1.json</c> dokümanının 200 döndürdüğünü ve yolları <c>v1</c> önekiyle (ör. <c>/v1/health</c>)
    /// listelediğini doğrular. FastEndpoints'teki <c>RoutePrefix = "v1"</c> ayarı dokümana yansımazsa dokümandaki yollar
    /// gerçek yollarla uyuşmaz; Scalar'dan ya da dokümandan üretilen istemcilerden yapılan çağrılar 404 alır.
    /// </summary>
    [Fact]
    public async Task OpenApi_document_lists_endpoints_with_their_v1_routes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        json.RootElement.GetProperty("paths").TryGetProperty("/v1/health", out _).ShouldBeTrue();
    }

    /// <summary>
    /// Etkileşimli API arayüzünün (<c>/scalar</c>) HTML olarak sunulduğunu doğrular. <c>Program.cs</c> arayüzü ortam
    /// koşulu olmadan eşler, bu yüzden Testing ortamında da erişilebilir olmalıdır; README'deki hızlı başlangıç API'yi bu
    /// sayfa üzerinden tanıtır.
    /// </summary>
    [Fact]
    public async Task Scalar_reference_ui_is_served()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }
}
