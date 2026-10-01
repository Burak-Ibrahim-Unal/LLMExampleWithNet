using Knowledge.Application.Contracts;

namespace SupportAssistant.Eval;

public sealed record EvalSuite(IReadOnlyList<EvalQuestion> Questions);

/// <param name="Category">normal | cevapsiz | celiskili</param>
/// <param name="ExpectedAnswer">Human-readable reference answer shown next to the actual answer in the report.</param>
public sealed record EvalQuestion(string Id, string Category, string Question, string ExpectedAnswer, EvalExpectation Expect);

/// <param name="Answerable">Whether the system must answer (true) or explicitly decline (false).</param>
/// <param name="SourcesAnyOf">At least one of these documents must be cited.</param>
/// <param name="SourcesAllOf">Every one of these documents must be cited.</param>
/// <param name="ForbiddenSources">None of these documents may be cited (outdated versions, an outdated FAQ).</param>
/// <param name="MustContain">Each inner list is an OR group; every group needs one phrase in the answer.</param>
/// <param name="MustNotContain">Phrases that must not appear in the answer (e.g. the outdated rule).</param>
/// <param name="DiscardedVersions">Outdated versions that must be reported in versionResolution.discarded.</param>
public sealed record EvalExpectation(
    bool Answerable,
    IReadOnlyList<string>? SourcesAnyOf = null,
    IReadOnlyList<string>? SourcesAllOf = null,
    IReadOnlyList<string>? ForbiddenSources = null,
    IReadOnlyList<IReadOnlyList<string>>? MustContain = null,
    IReadOnlyList<string>? MustNotContain = null,
    IReadOnlyList<string>? DiscardedVersions = null);

/// <summary>The API's ApiResult envelope as clients receive it.</summary>
public sealed record ApiEnvelope<T>(bool Success, string Message, T? Data, int StatusCode);

public sealed record CheckResult(string Name, bool Passed, string Detail);

/// <param name="LexicalHit">Expected source among the top search results with BM25 only (null: no expected source).</param>
/// <param name="HybridHit">Expected source among the top search results with BM25 + vectors.</param>
public sealed record QuestionResult(
    EvalQuestion Question,
    int StatusCode,
    string Message,
    AnswerDto? Answer,
    IReadOnlyList<CheckResult> Checks,
    long LatencyMs,
    bool? LexicalHit,
    bool? HybridHit)
{
    public bool Passed => Checks.Count > 0 && Checks.All(check => check.Passed);
}
