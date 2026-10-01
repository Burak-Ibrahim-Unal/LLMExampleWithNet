namespace Knowledge.Application.Abstractions;

/// <summary>Turns text into vectors for semantic search. Disabled when no embedding endpoint is configured.</summary>
public interface ITextEmbedder
{
    bool IsEnabled { get; }

    /// <summary>Recorded next to stored vectors: vectors from different models must never be compared.</summary>
    string ModelName { get; }

    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<DocumentEmbeddingInput> inputs, CancellationToken cancellationToken = default);

    Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken = default);
}

/// <param name="Title">Document title, available to models whose document prompt includes it.</param>
/// <param name="Text">The text to embed.</param>
public sealed record DocumentEmbeddingInput(string Title, string Text);
