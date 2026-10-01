using Knowledge.Application.Abstractions;
using Knowledge.Infrastructure.Embeddings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Embeddings;

public sealed class OpenAiCompatibleEmbedderTests
{
    /// <summary>The HTTP embedding endpoint is the external boundary; this records what would be sent.</summary>
    private sealed class RecordingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public List<List<string>> Batches { get; } = [];

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var batch = values.ToList();
            Batches.Add(batch);
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(batch.Select(_ => new Embedding<float>(new float[] { 3f, 4f }))));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private static OpenAiCompatibleEmbedder CreateEmbedder(RecordingGenerator generator, EmbeddingOptions options) =>
        new(generator, Options.Create(options));

    [Fact]
    public async Task Document_texts_get_the_document_prefix_with_their_title_and_are_sent_in_batches()
    {
        var generator = new RecordingGenerator();
        var embedder = CreateEmbedder(generator, new EmbeddingOptions { DocumentPrefix = "title: {title} | text: ", BatchSize = 2 });

        await embedder.EmbedDocumentsAsync(
            [new DocumentEmbeddingInput("İade", "bir"), new DocumentEmbeddingInput("İade", "iki"), new DocumentEmbeddingInput("Kargo", "üç")],
            TestContext.Current.CancellationToken);

        generator.Batches.Count.ShouldBe(2);
        generator.Batches[0].ShouldBe(["title: İade | text: bir", "title: İade | text: iki"]);
        generator.Batches[1].ShouldBe(["title: Kargo | text: üç"]);
    }

    [Fact]
    public async Task Query_text_gets_the_query_prefix()
    {
        var generator = new RecordingGenerator();
        var embedder = CreateEmbedder(generator, new EmbeddingOptions { QueryPrefix = "task: search result | query: " });

        await embedder.EmbedQueryAsync("iade süresi", TestContext.Current.CancellationToken);

        generator.Batches.ShouldHaveSingleItem().ShouldBe(["task: search result | query: iade süresi"]);
    }

    [Fact]
    public async Task Returned_vectors_are_scaled_to_unit_length()
    {
        var embedder = CreateEmbedder(new RecordingGenerator(), new EmbeddingOptions());

        var vector = await embedder.EmbedQueryAsync("iade", TestContext.Current.CancellationToken);

        vector.ShouldBe([0.6f, 0.8f], tolerance: 1e-6f);
    }
}
