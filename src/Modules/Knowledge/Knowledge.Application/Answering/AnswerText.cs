using System.Text.RegularExpressions;

namespace Knowledge.Application.Answering;

public static partial class AnswerText
{
    /// <summary>
    /// Removes source markers such as "[C1]", "[C1, "quote"]" or "(C2)" that models sometimes leave in the text:
    /// sources travel in their own field, the answer must read cleanly to the customer.
    /// </summary>
    public static string Clean(string answer, IReadOnlyCollection<string>? citedQuotes = null)
    {
        var withoutMarkers = SourceMarker().Replace(answer, string.Empty);
        return RepeatedSpaces().Replace(withoutMarkers, " ").Trim();
    }

    [GeneratedRegex(@"\s*(\[\s*C\d+[^\]]*\]|\(\s*C\d+(\s*,\s*C\d+)*\s*\))")]
    private static partial Regex SourceMarker();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex RepeatedSpaces();
}
