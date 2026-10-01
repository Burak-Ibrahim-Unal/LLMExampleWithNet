using System.Net;
using System.Text.Json;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

public sealed class HealthEndpointTests(SupportAssistantApiFactory factory) : IClassFixture<SupportAssistantApiFactory>
{
    [Fact]
    public async Task Health_is_served_under_the_v1_prefix_in_the_api_result_envelope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/health", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        json.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("statusCode").GetInt32().ShouldBe(200);
        json.RootElement.GetProperty("data").GetProperty("status").GetString().ShouldBe("ok");
    }
}
