namespace Knowledge.Application.Contracts;

/// <param name="RetrievalMode">"hybrid" (BM25 + vectors) or "lexical" (BM25 only).</param>
/// <param name="MaxDenseScore">Best cosine similarity among all chunks (0 in lexical mode).</param>
/// <param name="MaxLexicalCoverage">Best share of the query terms found in a single chunk.</param>
public sealed record SearchResultDto(
    string Query,
    string RetrievalMode,
    double MaxDenseScore,
    double MaxLexicalCoverage,
    IReadOnlyList<SearchHitDto> Hits);

/// <param name="Score">Reciprocal Rank Fusion score that orders the hits.</param>
public sealed record SearchHitDto(
    string DocumentId,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    string Status,
    string Category,
    string Section,
    string Content,
    double Score,
    double LexicalScore,
    double LexicalCoverage,
    double? DenseScore);
