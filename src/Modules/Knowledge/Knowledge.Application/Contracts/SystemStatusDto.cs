namespace Knowledge.Application.Contracts;

/// <param name="Status">"ok" when the index is built and a language model is configured, otherwise "degraded".</param>
public sealed record SystemStatusDto(string Status, IndexStatusDto Index, ComponentStatusDto Llm, ComponentStatusDto Embeddings);

public sealed record IndexStatusDto(bool Ready, int Documents, int Sections, string RetrievalMode, DateTime? BuiltAtUtc);

public sealed record ComponentStatusDto(bool Configured, string Model);
