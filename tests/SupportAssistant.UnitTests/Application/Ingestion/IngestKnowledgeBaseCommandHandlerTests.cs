using Knowledge.Application.Abstractions;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Commands.IngestKnowledgeBase;
using Knowledge.Application.Contracts;
using Knowledge.Application.Exceptions;
using Knowledge.Application.Options;
using Knowledge.Infrastructure.Persistence;
using Knowledge.Infrastructure.Search;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Persistence;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Ingestion;

/// <summary>Runs the handler against a real (in-memory) SQLite database and the real index; only the embedding server is faked.</summary>
public sealed class IngestKnowledgeBaseCommandHandlerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly StubKnowledgeBaseSource _source = new();
    private readonly FakeTextEmbedder _embedder = new();
    private KnowledgeIndex _index = null!;

    private static readonly SourceDocument ReturnPolicy = StubKnowledgeBaseSource.Document("iade", "hash-iade-1",
        new SourceSection("İade Süresi", "30 gün içinde iade edebilirsiniz."),
        new SourceSection("Para İadesi", "Ücret 5 iş günü içinde iade edilir."));

    private static readonly SourceDocument Shipping = StubKnowledgeBaseSource.Document("kargo", "hash-kargo-1",
        new SourceSection("Kargo Ücreti", "750 TL üzeri ücretsiz."));

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        _index = new KnowledgeIndex(_embedder, Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options,
        new EntityConfigurationAssemblyRegistry([Knowledge.Infrastructure.AssemblyReference.Assembly]));

    /// <summary>Each call uses a fresh DbContext, like a new request or an application restart.</summary>
    private async Task<Shared.Application.Common.ApiResult<IngestionSummaryDto>> IngestAsync()
    {
        await using var context = CreateContext();
        var handler = new IngestKnowledgeBaseCommandHandler(
            _source,
            new KnowledgeDocumentRepository(context),
            _embedder,
            _index,
            new KnowledgeBusinessRules(_index),
            NullLogger<IngestKnowledgeBaseCommandHandler>.Instance);

        return await handler.Handle(new IngestKnowledgeBaseCommand(), TestContext.Current.CancellationToken);
    }

    private async Task<List<(string SourceId, string Path, string Content, bool HasEmbedding)>> StoredChunksAsync()
    {
        await using var context = CreateContext();
        var documents = await new KnowledgeDocumentRepository(context).ListWithChunksAsync(TestContext.Current.CancellationToken);
        return documents
            .SelectMany(document => document.Chunks.OrderBy(chunk => chunk.Order)
                .Select(chunk => (document.SourceId, chunk.SectionPath, chunk.Content, chunk.Embedding is not null)))
            .OrderBy(chunk => chunk.SourceId)
            .ToList();
    }

    [Fact]
    public async Task First_ingestion_stores_every_section_embeds_it_and_builds_a_hybrid_index()
    {
        _source.Documents = [ReturnPolicy, Shipping];

        var result = await IngestAsync();

        result.Success.ShouldBeTrue();
        result.Data!.Added.ShouldBe(2);
        result.Data.Chunks.ShouldBe(3);
        result.Data.EmbeddedChunks.ShouldBe(3);
        result.Data.RetrievalMode.ShouldBe("hybrid");
        (await StoredChunksAsync()).ShouldBe(
        [
            ("iade", "İade Süresi", "30 gün içinde iade edebilirsiniz.", true),
            ("iade", "Para İadesi", "Ücret 5 iş günü içinde iade edilir.", true),
            ("kargo", "Kargo Ücreti", "750 TL üzeri ücretsiz.", true)
        ]);
        _index.Status.ChunkCount.ShouldBe(3);
    }

    [Fact]
    public async Task Re_ingesting_unchanged_documents_reuses_the_stored_embeddings()
    {
        _source.Documents = [ReturnPolicy, Shipping];
        await IngestAsync();

        var result = await IngestAsync();

        result.Data!.Unchanged.ShouldBe(2);
        result.Data.Added.ShouldBe(0);
        result.Data.EmbeddedChunks.ShouldBe(0);
        result.Data.RetrievalMode.ShouldBe("hybrid");
    }

    [Fact]
    public async Task A_changed_document_is_re_chunked_and_re_embedded_while_the_others_are_kept()
    {
        _source.Documents = [ReturnPolicy, Shipping];
        await IngestAsync();
        _source.Documents = [ReturnPolicy, StubKnowledgeBaseSource.Document("kargo", "hash-kargo-2", new SourceSection("Kargo Ücreti", "1000 TL üzeri ücretsiz."))];

        var result = await IngestAsync();

        result.Data!.Updated.ShouldBe(1);
        result.Data.Unchanged.ShouldBe(1);
        result.Data.EmbeddedChunks.ShouldBe(1);
        (await StoredChunksAsync()).Where(chunk => chunk.SourceId == "kargo")
            .ShouldBe([("kargo", "Kargo Ücreti", "1000 TL üzeri ücretsiz.", true)]);
    }

    [Fact]
    public async Task Documents_that_disappear_from_the_source_are_removed()
    {
        _source.Documents = [ReturnPolicy, Shipping];
        await IngestAsync();
        _source.Documents = [ReturnPolicy];

        var result = await IngestAsync();

        result.Data!.Removed.ShouldBe(1);
        (await StoredChunksAsync()).ShouldAllBe(chunk => chunk.SourceId == "iade");
        _index.Status.DocumentCount.ShouldBe(1);
    }

    [Fact]
    public async Task An_unreachable_embedding_server_leaves_a_working_lexical_index()
    {
        _source.Documents = [ReturnPolicy, Shipping];
        _embedder.Failure = new HttpRequestException("connection refused");

        var result = await IngestAsync();

        result.Success.ShouldBeTrue();
        result.Data!.EmbeddedChunks.ShouldBe(0);
        result.Data.RetrievalMode.ShouldBe("lexical");
        result.Data.Warning.ShouldNotBeNullOrWhiteSpace();
        _index.IsReady.ShouldBeTrue();
    }

    [Fact]
    public async Task A_malformed_knowledge_base_is_reported_as_unprocessable()
    {
        _source.Failure = new KnowledgeBaseFormatException("bozuk.md: zorunlu front matter alanı eksik: title");

        var result = await IngestAsync();

        result.Success.ShouldBeFalse();
        result.StatusCode.ShouldBe(422);
        result.Message.ShouldContain("bozuk.md");
        _index.IsReady.ShouldBeFalse();
    }

    [Theory]
    [InlineData("io")]
    [InlineData("access")]
    public async Task An_unreadable_knowledge_base_file_is_reported_as_unprocessable(string failure)
    {
        _source.Failure = failure == "io" ? new IOException("disk error") : new UnauthorizedAccessException("denied");

        var result = await IngestAsync();

        result.Success.ShouldBeFalse();
        result.StatusCode.ShouldBe(422);
        result.Message.ShouldBe(Shared.Application.Common.Messages.Knowledge.KnowledgeBaseUnreadable);
    }

    [Fact]
    public async Task Duplicate_document_ids_are_rejected()
    {
        _source.Documents = [ReturnPolicy, StubKnowledgeBaseSource.Document("iade", "hash-other", new SourceSection("A", "B"))];

        var result = await IngestAsync();

        result.Success.ShouldBeFalse();
        result.StatusCode.ShouldBe(422);
        result.Message.ShouldContain("iade");
    }

    [Fact]
    public async Task An_empty_knowledge_base_is_rejected()
    {
        _source.Documents = [];

        var result = await IngestAsync();

        result.Success.ShouldBeFalse();
        result.StatusCode.ShouldBe(422);
    }
}
