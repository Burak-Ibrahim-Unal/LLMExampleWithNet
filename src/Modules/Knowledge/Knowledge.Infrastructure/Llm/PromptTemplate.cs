using System.Text.RegularExpressions;

namespace Knowledge.Infrastructure.Llm;

/// <summary>
/// Prompt dosyasındaki <c>{ad}</c> biçimli yer tutucuları bulur ve doldurur.
/// </summary>
/// <remarks>
/// Doldurma tek geçişte yapılır: yer tutucular yalnızca kalıbın kendi metninde aranır, yerine konan değerler yeniden
/// taranmaz. Güvenilmez bir değer (ör. kendi başlığında <c>{question}</c> yazan bir doküman) bu yüzden kalıbın başka bir
/// yerini değiştiremez. Kalıpların hangi yer tutucuları taşıyacağı dosya yüklenirken doğrulanır
/// (<see cref="AnswerPromptTexts"/>); burada değeri verilmemiş bir yer tutucu programlama hatasıdır.
/// </remarks>
internal static partial class PromptTemplate
{
    /// <summary>Kalıptaki yer tutucuların adlarını döndürür (tekrarsız).</summary>
    public static HashSet<string> Placeholders(string template) =>
        Placeholder().Matches(template).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Kalıptaki her yer tutucuyu verilen değerle değiştirir. Değeri verilmemiş bir yer tutucu
    /// <see cref="InvalidOperationException"/> fırlatır.
    /// </summary>
    /// <param name="template">Prompt dosyasından gelen kalıp.</param>
    /// <param name="values">Yer tutucu adı ve değeri çiftleri.</param>
    public static string Fill(string template, params (string Name, string Value)[] values)
    {
        var lookup = values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

        return Placeholder().Replace(template, match => lookup.TryGetValue(match.Groups[1].Value, out var value)
            ? value
            : throw new InvalidOperationException($"No value was given for the prompt placeholder '{match.Value}'."));
    }

    /// <summary>Bir yer tutucu: süslü parantez içinde yalnızca harflerden oluşan bir ad (ör. <c>{question}</c>).</summary>
    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex Placeholder();
}
