namespace Knowledge.Application.Contracts;

/// <param name="Answerable">False when the documents do not contain enough information; <paramref name="Answer"/> then says so.</param>
/// <param name="Sources">Documents and sections the answer is based on, with the quoted text.</param>
/// <param name="VersionResolution">How conflicting versions of the same document were resolved.</param>
/// <param name="Conflicts">Disagreements between different documents reported by the model and checked against the precedence rule.</param>
/// <param name="MissingInformation">What the sources did not cover (partial answers and refusals).</param>
/// <param name="RefusalReason">Empty when answered; otherwise LowRelevance, ModelInsufficientContext or NoValidCitations.</param>
public sealed record AnswerDto(
    string Question,
    bool Answerable,
    string Answer,
    IReadOnlyList<AnswerSourceDto> Sources,
    VersionResolutionDto VersionResolution,
    IReadOnlyList<ConflictDto> Conflicts,
    string MissingInformation,
    string RefusalReason,
    AnswerDiagnosticsDto Diagnostics);

/// <param name="QuoteVerified">True when the quote occurs in the section text, i.e. it was not paraphrased or invented.</param>
public sealed record AnswerSourceDto(
    string DocumentId,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    string Status,
    string Category,
    string Section,
    string Quote,
    bool QuoteVerified);

/// <param name="Applied">True when an outdated version matched the question and was replaced by the version in effect.</param>
public sealed record VersionResolutionDto(
    bool Applied,
    string Rule,
    IReadOnlyList<VersionRefDto> Selected,
    IReadOnlyList<DiscardedVersionDto> Discarded);

public sealed record VersionRefDto(string DocumentId, string Title, string Version, DateOnly EffectiveDate);

public sealed record DiscardedVersionDto(string DocumentId, string Title, string Version, DateOnly EffectiveDate, string Reason);

/// <param name="RuleSatisfied">Whether the model's choice follows the precedence rule (authority first, then recency).</param>
public sealed record ConflictDto(
    string Topic,
    ConflictSourceDto Chosen,
    IReadOnlyList<ConflictSourceDto> Rejected,
    string Reason,
    bool RuleSatisfied);

public sealed record ConflictSourceDto(string DocumentId, string Version, DateOnly EffectiveDate, string Category, string Section);

/// <param name="CandidateDocumentIds">Documents retrieved before version resolution.</param>
/// <param name="Context">Sections given to the model after version resolution, with their labels.</param>
public sealed record AnswerDiagnosticsDto(
    string RetrievalMode,
    double MaxDenseScore,
    double MaxLexicalCoverage,
    IReadOnlyList<string> CandidateDocumentIds,
    IReadOnlyList<ContextSourceDto> Context,
    string Model,
    long LatencyMs,
    long? InputTokens,
    long? OutputTokens);

public sealed record ContextSourceDto(string Label, string DocumentId, string Version, string Section);
