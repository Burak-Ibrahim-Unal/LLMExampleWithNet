using System.Text.RegularExpressions;

namespace Knowledge.Application.Answering;

public static partial class AnswerText
{
    // Shorter cited fragments ("30 gün") are part of normal sentences and must never be cut out of the answer.
    private const int MinRemovableQuoteLength = 20;

    private static readonly (string Open, string Close)[] QuotationMarks = [("\"", "\""), ("“", "”"), ("«", "»")];

    /// <summary>
    /// Removes what models sometimes leave in the answer text despite the instructions: source markers such as
    /// "[C1]" or "(C2)" and quoted copies of the cited text. Sources travel in their own field; the answer must read
    /// cleanly to the customer.
    /// </summary>
    public static string Clean(string answer, IReadOnlyCollection<string>? citedQuotes = null)
    {
        var cleaned = SourceMarker().Replace(answer, string.Empty);

        foreach (var quote in citedQuotes ?? [])
        {
            var text = quote.Trim();

            if (text.Length < MinRemovableQuoteLength)
            {
                continue;
            }

            foreach (var (open, close) in QuotationMarks)
            {
                cleaned = cleaned.Replace(open + text + close, string.Empty, StringComparison.Ordinal);
            }
        }

        return RepeatedSpaces().Replace(cleaned, " ").Trim();
    }

    [GeneratedRegex(@"\s*(\[\s*C\d+[^\]]*\]|\(\s*C\d+(\s*,\s*C\d+)*\s*\))")]
    private static partial Regex SourceMarker();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex RepeatedSpaces();
}
