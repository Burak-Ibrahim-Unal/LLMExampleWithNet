using Knowledge.Application.Abstractions;

namespace SupportAssistant.UnitTests.TestDoubles;

/// <summary>Stands in for the remote embedding server; vectors are deterministic and configurable.</summary>
internal sealed class FakeTextEmbedder(string modelName = FakeTextEmbedder.DefaultModel, bool enabled = true) : ITextEmbedder
{
    public const string DefaultModel = "fake-embedding";

    public bool IsEnabled => enabled;

    public string ModelName => modelName;

    public Func<string, float[]> QueryVector { get; init; } = _ => [1f, 0f];

    public Exception? Failure { get; set; }

    public List<string> EmbeddedDocumentTexts { get; } = [];

    public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<DocumentEmbeddingInput> inputs, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        EmbeddedDocumentTexts.AddRange(inputs.Select(input => input.Text));
        IReadOnlyList<float[]> vectors = inputs.Select(input => new[] { input.Text.Length, 1f }).ToList();
        return Task.FromResult(vectors);
    }

    public Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        return Failure is not null ? Task.FromException<float[]>(Failure) : Task.FromResult(QueryVector(query));
    }
}
