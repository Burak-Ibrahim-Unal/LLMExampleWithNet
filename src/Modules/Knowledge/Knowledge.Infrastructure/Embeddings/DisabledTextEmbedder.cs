using Knowledge.Application.Abstractions;

namespace Knowledge.Infrastructure.Embeddings;

/// <summary>Used when no embedding endpoint is configured: the index runs in BM25-only mode.</summary>
public sealed class DisabledTextEmbedder : ITextEmbedder
{
    public bool IsEnabled => false;

    public string ModelName => string.Empty;

    public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<DocumentEmbeddingInput> inputs, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Embeddings are disabled.");

    public Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Embeddings are disabled.");
}
