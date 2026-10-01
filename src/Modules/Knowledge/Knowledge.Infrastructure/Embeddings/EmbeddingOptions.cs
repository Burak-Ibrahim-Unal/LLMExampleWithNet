namespace Knowledge.Infrastructure.Embeddings;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embeddings";

    /// <summary>OpenAI-compatible endpoint, e.g. http://localhost:1235/v1. Empty disables vector search.</summary>
    public string? BaseUrl { get; set; }

    public string ApiKey { get; set; } = "local";

    public string Model { get; set; } = "bge-m3";

    /// <summary>Text prepended to queries; some models (EmbeddingGemma, e5) expect task prompts. bge-m3 needs none.</summary>
    public string QueryPrefix { get; set; } = string.Empty;

    /// <summary>Text prepended to documents; "{title}" is replaced with the document title.</summary>
    public string DocumentPrefix { get; set; } = string.Empty;

    public int BatchSize { get; set; } = 16;

    public int TimeoutSeconds { get; set; } = 60;
}
