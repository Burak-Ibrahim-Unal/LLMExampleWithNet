namespace Knowledge.Infrastructure.Llm;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>OpenAI-compatible endpoint, e.g. http://localhost:1234/v1 (llama.cpp server, LM Studio, Ollama, OpenAI, Gemini).</summary>
    public string? BaseUrl { get; set; }

    public string ApiKey { get; set; } = "local";

    /// <summary>Model name; single-model servers such as llama.cpp ignore it, it is still logged and reported.</summary>
    public string ChatModel { get; set; } = "gemma-4-26b-a4b-it";

    /// <summary>
    /// Thinking switch for chat templates that support it (sent as chat_template_kwargs.enable_thinking);
    /// null leaves the server default.
    /// </summary>
    public bool? EnableThinking { get; set; }

    public float? Temperature { get; set; } = 0f;

    public long? Seed { get; set; } = 42;

    /// <summary>Generous so that reasoning tokens of thinking models cannot crowd out the JSON answer.</summary>
    public int MaxOutputTokens { get; set; } = 4096;

    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Send a JSON schema (response_format); when false the schema is described in the prompt instead.</summary>
    public bool UseJsonSchema { get; set; } = true;
}
