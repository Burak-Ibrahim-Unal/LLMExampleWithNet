using Knowledge.Application.Text;

namespace Knowledge.Infrastructure.Search;

/// <summary>
/// Turns text into search terms: Turkish normalization, stopword removal and fixed-prefix stemming.
/// Turkish is agglutinative ("iade", "iadesi", "iadeler"); keeping the first five characters (F5) is a
/// simple stemming method that Turkish IR studies found competitive with morphological analysis.
/// </summary>
public static class SearchTokenizer
{
    public const int StemLength = 5;

    // Folded (ASCII) forms of frequent function words and question particles.
    private static readonly HashSet<string> Stopwords =
    [
        "ve", "veya", "ile", "ya", "bir", "bu", "su", "o", "da", "de", "ki", "mi", "mu", "ne",
        "icin", "icinde", "gibi", "daha", "en", "cok", "ama", "ancak", "fakat", "ise", "hem", "her",
        "nasil", "neden", "hangi", "kac", "olan", "olarak", "var", "yok", "ben", "sen", "biz", "siz",
        "benim", "bana", "beni", "sizin", "size", "sizi", "miyim", "misin", "misiniz", "musunuz",
        "midir", "mudur", "nedir", "nerede", "nereden", "kadar", "sonra", "once", "eger", "yani", "gore"
    ];

    public static IReadOnlyList<string> Tokenize(string text)
    {
        var terms = new List<string>();

        foreach (var word in TurkishTextNormalizer.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Stopwords.Contains(word) || (word.Length == 1 && !char.IsDigit(word[0])))
            {
                continue;
            }

            terms.Add(word.Length > StemLength ? word[..StemLength] : word);
        }

        return terms;
    }
}
