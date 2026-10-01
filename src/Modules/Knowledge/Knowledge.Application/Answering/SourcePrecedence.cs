using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;

namespace Knowledge.Application.Answering;

/// <summary>Which source wins when two different documents disagree.</summary>
public static class SourcePrecedence
{
    public const string Rule = "Politika ve prosedür dokümanları kılavuzlardan, kılavuzlar SSS'den önceliklidir; aynı türde yürürlük tarihi daha yeni olan geçerlidir.";

    public static bool Outranks(IndexedChunk candidate, IndexedChunk other)
    {
        var candidateRank = AuthorityRank(candidate.Category);
        var otherRank = AuthorityRank(other.Category);

        return candidateRank != otherRank
            ? candidateRank < otherRank
            : candidate.EffectiveDate >= other.EffectiveDate;
    }

    // Lower is more authoritative: a policy owner signs off a policy, an FAQ is a summary that can lag behind it.
    private static int AuthorityRank(DocumentCategory category) => category switch
    {
        DocumentCategory.Policy or DocumentCategory.Procedure => 0,
        DocumentCategory.Guide => 1,
        _ => 2
    };
}
