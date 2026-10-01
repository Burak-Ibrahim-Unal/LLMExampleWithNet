namespace Knowledge.Application.Contracts;

/// <param name="RetrievalMode">"hybrid" or "lexical" after this ingestion.</param>
/// <param name="Warning">Set when ingestion succeeded in a degraded mode (e.g. embedding server unreachable).</param>
public sealed record IngestionSummaryDto(
    int Documents,
    int Chunks,
    int Added,
    int Updated,
    int Removed,
    int Unchanged,
    int EmbeddedChunks,
    string RetrievalMode,
    string? Warning);
