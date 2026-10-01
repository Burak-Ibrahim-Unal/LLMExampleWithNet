namespace Knowledge.Application.Answering;

internal static class SourceLabel
{
    /// <summary>Accepts the variants models produce for a label: "C1", "c1", "[C1]", "1".</summary>
    public static string Normalize(string label)
    {
        var trimmed = label.Trim().Trim('[', ']', '(', ')').Trim();
        return trimmed.Length > 0 && trimmed.All(char.IsDigit) ? $"C{trimmed}" : trimmed.ToUpperInvariant();
    }
}
