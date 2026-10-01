using System.Globalization;
using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;

namespace Knowledge.Application.Answering;

/// <summary>
/// Deterministic version conflict resolution. When retrieval returns several versions of the same document
/// (same documentKey), only the version in effect is passed to the language model; the others are reported
/// with the reason they were dropped. Deciding this in code — not in the prompt — makes it explainable,
/// testable and immune to the model mixing an old rule into the answer.
/// </summary>
public sealed class VersionResolver(TimeProvider timeProvider)
{
    public const string Rule = "Aynı doküman ailesinde, yürürlük tarihi bugün veya daha önce olan en yeni sürüm seçilir; 'superseded' işaretli sürüm seçilmez.";

    public VersionResolution Resolve(IReadOnlyList<SearchHit> candidates, Func<string, IReadOnlyList<DocumentVersion>> versionsOf)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var families = new Dictionary<string, (IReadOnlyList<DocumentVersion> Versions, DocumentVersion? Current)>(StringComparer.Ordinal);

        foreach (var documentKey in candidates.Select(hit => hit.Chunk.DocumentKey).Distinct(StringComparer.Ordinal))
        {
            var versions = versionsOf(documentKey);

            // A single-version document has nothing to conflict with.
            if (versions.Count > 1)
            {
                families[documentKey] = (versions, SelectCurrent(versions, today));
            }
        }

        var kept = new List<SearchHit>();
        var discarded = new Dictionary<string, DiscardedVersion>(StringComparer.Ordinal);

        foreach (var hit in candidates)
        {
            if (!families.TryGetValue(hit.Chunk.DocumentKey, out var family) || hit.Chunk.DocumentId == family.Current?.DocumentId)
            {
                kept.Add(hit);
                continue;
            }

            if (!discarded.ContainsKey(hit.Chunk.DocumentId))
            {
                var version = family.Versions.First(candidate => candidate.DocumentId == hit.Chunk.DocumentId);
                discarded[hit.Chunk.DocumentId] = new DiscardedVersion(version, Reason(version, family.Current, today));
            }
        }

        var selected = families.Values.Where(family => family.Current is not null).Select(family => family.Current!).ToList();
        var needsSubstitution = selected.Where(version => kept.All(hit => hit.Chunk.DocumentId != version.DocumentId)).ToList();

        return new VersionResolution(kept, selected, discarded.Values.ToList(), needsSubstitution);
    }

    private static DocumentVersion? SelectCurrent(IReadOnlyList<DocumentVersion> versions, DateOnly today)
    {
        return versions
            .Where(version => version.Status != DocumentStatus.Superseded && version.EffectiveDate <= today)
            .OrderByDescending(version => version.EffectiveDate)
            .ThenByDescending(version => System.Version.TryParse(version.Version, out var number) ? number : new System.Version(0, 0))
            .FirstOrDefault();
    }

    private static string Reason(DocumentVersion version, DocumentVersion? current, DateOnly today)
    {
        if (version.EffectiveDate > today)
        {
            return $"Yürürlük tarihi ({Format(version.EffectiveDate)}) henüz gelmedi.";
        }

        return current is null
            ? "Bu dokümanın yürürlükte bir sürümü yok."
            : $"{current.Version} sürümü ({Format(current.EffectiveDate)}) tarafından geçersiz kılındı.";
    }

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <param name="Kept">Candidates from the version in effect (or from single-version documents), in their original order.</param>
/// <param name="Selected">Version chosen for each multi-version family that appeared among the candidates.</param>
/// <param name="Discarded">Versions removed from the candidates, with the reason.</param>
/// <param name="NeedsSubstitution">Selected versions none of whose sections were retrieved; their best sections must be fetched.</param>
public sealed record VersionResolution(
    IReadOnlyList<SearchHit> Kept,
    IReadOnlyList<DocumentVersion> Selected,
    IReadOnlyList<DiscardedVersion> Discarded,
    IReadOnlyList<DocumentVersion> NeedsSubstitution)
{
    public bool Applied => Discarded.Count > 0;
}

public sealed record DiscardedVersion(DocumentVersion Version, string Reason);
