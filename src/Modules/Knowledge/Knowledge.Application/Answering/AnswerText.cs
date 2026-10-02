using System.Text.RegularExpressions;

namespace Knowledge.Application.Answering;

/// <summary>
/// Modelin yanıt metnini müşteriye iletilebilecek hâle getiren temizleyici.
/// </summary>
/// <remarks>
/// Kaynaklar yanıtta kendi alanında (<c>sources</c>) taşınır; yanıt metni, temsilcinin müşteriye olduğu gibi
/// iletebileceği kadar temiz olmalıdır. Prompt bunu açıkça istese de modeller zaman zaman "[C1]" gibi işaretleri ya da
/// alıntının kendisini metne kopyalar; bu yüzden yalnızca prompt'a güvenilmez, metin sunucu tarafında temizlenir.
/// </remarks>
public static partial class AnswerText
{
    /// <summary>
    /// Yanıttan silinebilecek alıntının en kısa uzunluğu. "30 gün" gibi daha kısa atıf parçaları normal cümlelerin
    /// parçasıdır ve yanıttan asla kesilmemelidir; aksi hâlde yanıtın kendisi anlamını yitirirdi.
    /// </summary>
    private const int MinRemovableQuoteLength = 20;

    /// <summary>
    /// Alıntı kopyasını sarabilecek tırnak çiftleri: düz ASCII tırnak, kıvrık tırnak (“ ”) ve açılı tırnak (« »).
    /// Yalnızca bu çiftlerle sarılmış kopyalar silinir; tırnaksız geçen aynı ifade yanıtın kendi cümlesi olabilir.
    /// </summary>
    private static readonly (string Open, string Close)[] QuotationMarks = [("\"", "\""), ("“", "”"), ("«", "»")];

    /// <summary>
    /// Modellerin talimata rağmen yanıt metninde bıraktıklarını siler: "[C1]" veya "(C2)" gibi kaynak işaretlerini ve atıf
    /// yapılan metnin tırnak içindeki kopyalarını. Kaynaklar kendi alanlarında taşınır; yanıt müşteriye temiz okunmalıdır.
    /// </summary>
    /// <remarks>
    /// Önce kaynak işaretleri, sonra en az <c>MinRemovableQuoteLength</c> karakterlik alıntıların tırnaklı kopyaları silinir;
    /// ardından silmelerin bıraktığı çoklu boşluklar teke indirilir ve metin kırpılır. Temizlik sonunda metin boş kalırsa
    /// handler atıfların alıntılarını yanıt olarak kullanır; o da boşsa yanıt <c>NoValidCitations</c> ile reddedilir.
    /// </remarks>
    /// <param name="answer">Modelin ürettiği yanıt metni.</param>
    /// <param name="citedQuotes">
    /// <c>CitationValidator</c>'ın kabul ettiği atıfların alıntıları; null ise yalnızca kaynak işaretleri silinir.
    /// </param>
    /// <returns>Temizlenmiş ve kırpılmış yanıt metni; boş olabilir.</returns>
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

    /// <summary>
    /// Kaynak işaretlerini önlerindeki boşlukla birlikte bulur: büyük harf C ile yazılmış "[C1]", "[C1, "alıntı…"]" gibi
    /// köşeli parantezli biçimler ve "(C2)", "(C1, C3)" gibi parantez içindeki saf etiket listeleri.
    /// </summary>
    /// <remarks>
    /// Öndeki boşluk eşleşmeye dahildir; böylece "edebilirsiniz [C1]." ifadesi "edebilirsiniz." olur. Köşeli parantez Türkçe
    /// düz metinde neredeyse hiç kullanılmadığından parantezin tüm içeriği silinir; normal parantez ise sık kullanıldığı
    /// için yalnızca saf etiket listeleri silinir, "(C2 numaralı adım)" gibi açıklamalara dokunulmaz. Regex kaynak üreteciyle
    /// derleme zamanında üretilir.
    /// </remarks>
    [GeneratedRegex(@"\s*(\[\s*C\d+[^\]]*\]|\(\s*C\d+(\s*,\s*C\d+)*\s*\))")]
    private static partial Regex SourceMarker();

    /// <summary>
    /// Silmelerden sonra kalan ardışık boşluk ve sekmeleri bulur. Satır sonları kapsam dışıdır; modelin paragraf ve madde
    /// yapısı korunur.
    /// </summary>
    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex RepeatedSpaces();
}
