using Knowledge.Application.Abstractions;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Commands.IngestKnowledgeBase;
using Knowledge.Application.Contracts;
using Knowledge.Application.Options;
using Knowledge.Infrastructure.Persistence;
using Knowledge.Infrastructure.Search;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Application.Common;
using Shared.Infrastructure.Persistence;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Ingestion;

/// <summary>Two reindex requests at once, each with its own connection to a file database — as in the running API.</summary>
public sealed class ConcurrentIngestionTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"ingest-{Guid.NewGuid():N}.db");
    private readonly StubKnowledgeBaseSource _source = new();
    private readonly FakeTextEmbedder _embedder = new();
    private KnowledgeIndex _index = null!;

    public async ValueTask InitializeAsync()
    {
        _index = new KnowledgeIndex(_embedder, Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
        return ValueTask.CompletedTask;
    }

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_databasePath}").Options,
        new EntityConfigurationAssemblyRegistry([Knowledge.Infrastructure.AssemblyReference.Assembly]));

    private async Task<ApiResult<IngestionSummaryDto>> IngestAsync()
    {
        await using var context = CreateContext();
        var handler = new IngestKnowledgeBaseCommandHandler(
            _source, new KnowledgeDocumentRepository(context), _embedder, _index, new KnowledgeBusinessRules(_index),
            NullLogger<IngestKnowledgeBaseCommandHandler>.Instance);

        return await handler.Handle(new IngestKnowledgeBaseCommand(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Concurrent_reindex_requests_are_serialised_instead_of_failing()
    {
        _source.Documents = [StubKnowledgeBaseSource.Document("iade", "hash-1", new SourceSection("İade Süresi", "14 gün."), new SourceSection("Kargo", "Müşteri öder."))];
        await IngestAsync();
        _source.Documents = [StubKnowledgeBaseSource.Document("iade", "hash-2", new SourceSection("İade Süresi", "30 gün."), new SourceSection("Kargo", "Ücretsiz."))];

        // While one request waits for embeddings, the others read the same stored state and race to save.
        _embedder.Delay = TimeSpan.FromMilliseconds(300);
        var results = await Task.WhenAll(IngestAsync(), IngestAsync(), IngestAsync());

        results.ShouldAllBe(result => result.Success);
        results.Sum(result => result.Data!.Updated).ShouldBe(1);
        results.Sum(result => result.Data!.Unchanged).ShouldBe(2);
    }
}
