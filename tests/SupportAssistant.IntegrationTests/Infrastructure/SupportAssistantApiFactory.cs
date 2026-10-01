using Knowledge.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SupportAssistant.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in-process with an isolated, throw-away SQLite database and a small fixture knowledge base.
/// The "Testing" environment keeps the developer's .env file (remote LLM endpoints) out of the tests, and the
/// embedding endpoint is left empty so retrieval runs in BM25-only mode without network access.
/// </summary>
public class SupportAssistantApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"supportassistant-tests-{Guid.NewGuid():N}.db");

    protected virtual string KnowledgeBasePath => Path.Combine(AppContext.BaseDirectory, "TestData", "knowledge-base");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}",
                ["KnowledgeBase:Path"] = KnowledgeBasePath,
                ["Embeddings:BaseUrl"] = string.Empty,
                ["Llm:BaseUrl"] = string.Empty
            });
        });

        builder.ConfigureTestServices(services => services.AddSingleton<IGroundedAnswerGenerator, FakeAnswerGenerator>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}

/// <summary>Points the API at a folder that does not exist, so startup ingestion fails.</summary>
public sealed class MissingKnowledgeBaseApiFactory : SupportAssistantApiFactory
{
    protected override string KnowledgeBasePath => Path.Combine(AppContext.BaseDirectory, "TestData", "does-not-exist");
}
