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

    /// <summary>Simulated network latency of the embedding server.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<DocumentEmbeddingInput> inputs, CancellationToken cancellationToken = default)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        lock (EmbeddedDocumentTexts)
        {
            EmbeddedDocumentTexts.AddRange(inputs.Select(input => input.Text));
        }

        return inputs.Select(input => new[] { input.Text.Length, 1f }).ToList();
    }

    public Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        return Failure is not null ? Task.FromException<float[]>(Failure) : Task.FromResult(QueryVector(query));
    }
}
