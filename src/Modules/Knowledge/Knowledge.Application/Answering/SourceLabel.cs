namespace Knowledge.Application.Answering;

/// <summary>Modelin yazdığı kaynak etiketini bağlamdaki biçime ("C1") getirir.</summary>
/// <remarks>
/// Atıf doğrulama (<c>CitationValidator</c>) ve çelişki denetimi (soru handler'ı) aynı kuralı kullansın diye tek yerde
/// tutulur. Yalnızca bu derlemede kullanıldığı için <c>internal</c>'dır.
/// </remarks>
internal static class SourceLabel
{
    /// <summary>Modellerin bir etiket için ürettiği varyantları kabul eder: "C1", "c1", "[C1]", "1".</summary>
    /// <remarks>
    /// Boşluklar ve çevreleyen köşeli/normal parantezler atılır; yalnızca rakamlardan oluşan değerin başına "C" eklenir,
    /// diğerleri büyük harfe çevrilir. Biçim farkı yüzünden geçerli bir atfı kaybetmek gereksiz bir ret üretirdi; buna
    /// karşın tanınmayan bir değer bağlamda karşılığı olmadığı için yine elenir, yani esneklik bir güvenlik açığı yaratmaz.
    /// </remarks>
    public static string Normalize(string label)
    {
        var trimmed = label.Trim().Trim('[', ']', '(', ')').Trim();
        return trimmed.Length > 0 && trimmed.All(char.IsDigit) ? $"C{trimmed}" : trimmed.ToUpperInvariant();
    }
}
