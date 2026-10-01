using Knowledge.Application.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Embeddings;

/// <summary>
/// Embeds text through any OpenAI-compatible /v1/embeddings endpoint (llama.cpp server, LM Studio, Ollama,
/// OpenAI, Gemini) via Microsoft.Extensions.AI. Vectors are scaled to unit length so cosine similarity is comparable
/// regardless of the provider.
/// </summary>
public sealed class OpenAiCompatibleEmbedder(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    IOptions<EmbeddingOptions> options) : ITextEmbedder
{
    public bool IsEnabled => true;

    public string ModelName => options.Value.Model;

    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<DocumentEmbeddingInput> inputs, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var texts = inputs
            .Select(input => settings.DocumentPrefix.Replace("{title}", input.Title, StringComparison.Ordinal) + input.Text)
            .ToList();

        var vectors = new List<float[]>(texts.Count);

        foreach (var batch in texts.Chunk(Math.Max(1, settings.BatchSize)))
        {
            vectors.AddRange(await EmbedAsync(batch, cancellationToken));
        }

        return vectors;
    }

    public async Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        var vectors = await EmbedAsync([options.Value.QueryPrefix + query], cancellationToken);
        return vectors[0];
    }

    private async Task<IReadOnlyList<float[]>> EmbedAsync(string[] texts, CancellationToken cancellationToken)
    {
        var embeddings = await generator.GenerateAsync(texts, cancellationToken: cancellationToken);

        if (embeddings.Count != texts.Length)
        {
            throw new InvalidOperationException($"The embedding endpoint returned {embeddings.Count} vectors for {texts.Length} inputs.");
        }

        return embeddings.Select(embedding => ToUnitLength(embedding.Vector.Span)).ToList();
    }

    private static float[] ToUnitLength(ReadOnlySpan<float> vector)
    {
        var result = vector.ToArray();
        var norm = Math.Sqrt(result.Sum(value => (double)value * value));

        if (norm == 0)
        {
            return result;
        }

        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (float)(result[i] / norm);
        }

        return result;
    }
}
