using System.Net;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

public sealed class HealthEndpointTests(SupportAssistantApiFactory factory) : IClassFixture<SupportAssistantApiFactory>
{
    [Fact]
    public async Task Health_reports_the_index_and_model_status_in_the_api_result_envelope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await ApiResponse.ReadAsync(await client.GetAsync("/v1/health", cancellationToken), cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Success.ShouldBeTrue();
        response.Data.GetProperty("status").GetString().ShouldBe("ok");
        var index = response.Data.GetProperty("index");
        index.GetProperty("ready").GetBoolean().ShouldBeTrue();
        index.GetProperty("documents").GetInt32().ShouldBe(3);
        index.GetProperty("retrievalMode").GetString().ShouldBe("lexical");
        response.Data.GetProperty("llm").GetProperty("configured").GetBoolean().ShouldBeTrue();
        response.Data.GetProperty("embeddings").GetProperty("configured").GetBoolean().ShouldBeFalse();
    }
}

public sealed class DegradedHealthTests(MissingKnowledgeBaseApiFactory factory) : IClassFixture<MissingKnowledgeBaseApiFactory>
{
    [Fact]
    public async Task Health_is_degraded_while_the_index_is_not_built()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await ApiResponse.ReadAsync(await client.GetAsync("/v1/health", cancellationToken), cancellationToken);

        response.Data.GetProperty("status").GetString().ShouldBe("degraded");
        response.Data.GetProperty("index").GetProperty("ready").GetBoolean().ShouldBeFalse();
    }
}
