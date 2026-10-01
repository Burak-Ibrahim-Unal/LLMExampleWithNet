using System.Globalization;
using System.Text;

namespace Knowledge.Application.Text;

public static class TurkishTextNormalizer
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// Lower-cases with Turkish casing rules, folds Turkish letters to ASCII (ç→c, ğ→g, ı→i, ö→o, ş→s, ü→u)
    /// and turns every run of non letter/digit characters into a single space:
    /// "İade süresi kaç gün?" → "iade suresi kac gun". Users often type without Turkish characters,
    /// so both spellings must meet in the same form.
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // FormD splits "ş" into "s" + combining cedilla, "ü" into "u" + diaeresis, and so on.
        var decomposed = text.ToLower(Turkish).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // Dotless ı has no decomposition, so it is folded explicitly.
            var folded = character == 'ı' ? 'i' : character;

            if (!char.IsLetterOrDigit(folded))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace && builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(folded);
            pendingSpace = false;
        }

        return builder.ToString();
    }
}
