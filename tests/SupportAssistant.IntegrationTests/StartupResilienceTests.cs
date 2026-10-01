using System.Net;
using Knowledge.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

/// <summary>A knowledge base that fails unexpectedly while being read must not take the whole API down.</summary>
public sealed class FailingKnowledgeBaseApiFactory : SupportAssistantApiFactory
{
    private sealed class FailingSource : IKnowledgeBaseSource
    {
        public Task<IReadOnlyList<SourceDocument>> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected failure while reading the knowledge base");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IKnowledgeBaseSource, FailingSource>());
    }
}

public sealed class StartupResilienceTests(FailingKnowledgeBaseApiFactory factory) : IClassFixture<FailingKnowledgeBaseApiFactory>
{
    [Fact]
    public async Task The_api_starts_and_reports_degraded_health_when_indexing_fails_at_startup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await ApiResponse.ReadAsync(await client.GetAsync("/v1/health", cancellationToken), cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("status").GetString().ShouldBe("degraded");
        response.Data.GetProperty("index").GetProperty("ready").GetBoolean().ShouldBeFalse();
    }
}
