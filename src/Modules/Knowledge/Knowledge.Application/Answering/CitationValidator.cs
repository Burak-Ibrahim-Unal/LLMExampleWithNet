using Knowledge.Application.Abstractions;
using Knowledge.Application.Text;

namespace Knowledge.Application.Answering;

/// <summary>
/// Gates 2–3 helper: keeps only citations that point at a source the model was actually given, and checks
/// whether each quote really occurs in that source. A model cannot cite its way out of the provided context.
/// </summary>
public static class CitationValidator
{
    private static readonly string[] Ellipses = ["...", "…"];

    public static IReadOnlyList<ValidatedCitation> Validate(IReadOnlyList<GeneratedCitation> citations, IReadOnlyList<ContextChunk> context)
    {
        var sourcesByLabel = context.ToDictionary(source => source.Label, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<(string Label, string Quote)>();
        var validated = new List<ValidatedCitation>();

        foreach (var citation in citations)
        {
            if (!sourcesByLabel.TryGetValue(SourceLabel.Normalize(citation.ChunkLabel), out var source))
            {
                continue;
            }

            var quote = citation.Quote.Trim();

            if (seen.Add((source.Label, quote)))
            {
                validated.Add(new ValidatedCitation(source, quote, IsVerbatim(quote, source.Chunk.Content)));
            }
        }

        return validated;
    }

    // Compared in normalized form (case, Turkish letters, punctuation); "..." elisions are allowed between fragments.
    private static bool IsVerbatim(string quote, string sourceText)
    {
        var fragments = quote
            .Split(Ellipses, StringSplitOptions.RemoveEmptyEntries)
            .Select(TurkishTextNormalizer.Normalize)
            .Where(fragment => fragment.Length > 0)
            .ToList();

        if (fragments.Count == 0)
        {
            return false;
        }

        var normalizedSource = TurkishTextNormalizer.Normalize(sourceText);
        return fragments.All(fragment => normalizedSource.Contains(fragment, StringComparison.Ordinal));
    }
}

/// <param name="QuoteVerified">True when the quote really occurs in the cited section (ignoring case and Turkish characters).</param>
public sealed record ValidatedCitation(ContextChunk Source, string Quote, bool QuoteVerified);
