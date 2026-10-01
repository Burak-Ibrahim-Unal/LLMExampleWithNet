using System.Net;
using System.Text.Json;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

public sealed class ApiDocumentationTests(SupportAssistantApiFactory factory) : IClassFixture<SupportAssistantApiFactory>
{
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
