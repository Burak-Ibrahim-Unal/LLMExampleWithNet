using Knowledge.Application.Contracts;
using Knowledge.Application.Text;

namespace SupportAssistant.Eval;

/// <summary>Deterministic checks of one answer against its expectation; no model is involved in grading.</summary>
public static class EvalChecks
{
    public static IReadOnlyList<CheckResult> Evaluate(EvalExpectation expect, AnswerDto answer)
    {
        var checks = new List<CheckResult>
        {
            new("yanıtlanabilirlik", answer.Answerable == expect.Answerable,
                $"beklenen: {(expect.Answerable ? "yanıt" : "bilgi yok")}, gerçek: {(answer.Answerable ? "yanıt" : $"bilgi yok ({answer.RefusalReason})")}")
        };

        // A refusal is judged on the refusal alone; content checks of a refused (or wrongly answered) question say nothing.
        if (!expect.Answerable || !answer.Answerable)
        {
            return checks;
        }

        var cited = answer.Sources.Select(source => source.DocumentId).Distinct(StringComparer.Ordinal).ToList();

        if (expect.SourcesAnyOf is { Count: > 0 } anyOf)
        {
            checks.Add(new CheckResult("kaynak", anyOf.Any(cited.Contains), $"beklenen: {string.Join(" | ", anyOf)}; atıf: {Join(cited)}"));
        }

        if (expect.SourcesAllOf is { Count: > 0 } allOf)
        {
            checks.Add(new CheckResult("kaynaklar", allOf.All(cited.Contains), $"beklenen: {string.Join(" + ", allOf)}; atıf: {Join(cited)}"));
        }

        if (expect.ForbiddenSources is { Count: > 0 } forbidden)
        {
            var used = forbidden.Where(cited.Contains).ToList();
            checks.Add(new CheckResult("yasak kaynak yok", used.Count == 0, used.Count == 0 ? "yok" : $"atıf yapılmış: {Join(used)}"));
        }

        foreach (var group in expect.MustContain ?? [])
        {
            checks.Add(new CheckResult("içerik", group.Any(phrase => ContainsPhrase(answer.Answer, phrase)), $"'{string.Join("' | '", group)}'"));
        }

        foreach (var phrase in expect.MustNotContain ?? [])
        {
            checks.Add(new CheckResult("yasak ifade", !ContainsPhrase(answer.Answer, phrase), $"'{phrase}'"));
        }

        if (expect.DiscardedVersions is { Count: > 0 } discarded)
        {
            var reported = answer.VersionResolution.Discarded.Select(version => version.DocumentId).ToList();
            checks.Add(new CheckResult("eski sürüm elendi", discarded.All(reported.Contains), $"beklenen: {Join(discarded)}; raporlanan: {Join(reported)}"));
        }

        return checks;
    }

    /// <summary>
    /// Case-, punctuation- and Turkish-character-insensitive match that must start at a word boundary:
    /// "ücretsiz" matches "ücretsizdir" (Turkish suffixes), "30 gün" does not match "300 gün".
    /// </summary>
    public static bool ContainsPhrase(string text, string phrase)
    {
        var normalizedPhrase = TurkishTextNormalizer.Normalize(phrase);

        return normalizedPhrase.Length > 0
            && (" " + TurkishTextNormalizer.Normalize(text)).Contains(" " + normalizedPhrase, StringComparison.Ordinal);
    }

    /// <summary>Whether retrieval surfaced the expected source(s); null for questions without an expected source.</summary>
    public static bool? RetrievalHit(EvalExpectation expect, IReadOnlyCollection<string> retrievedDocumentIds)
    {
        if (expect.SourcesAllOf is { Count: > 0 } allOf)
        {
            return allOf.All(retrievedDocumentIds.Contains);
        }

        if (expect.SourcesAnyOf is { Count: > 0 } anyOf)
        {
            return anyOf.Any(retrievedDocumentIds.Contains);
        }

        return null;
    }

    private static string Join(IReadOnlyCollection<string> values) => values.Count == 0 ? "—" : string.Join(", ", values);
}
