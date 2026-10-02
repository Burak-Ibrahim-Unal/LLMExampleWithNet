using System.Globalization;
using System.Text;

namespace Knowledge.Application.Text;

/// <summary>
/// Türkçe metni arama ve karşılaştırma için tek bir biçime indirir. BM25 terimleri (<c>SearchTokenizer</c>), atıf
/// alıntısı doğrulaması (<c>CitationValidator</c>) ve değerlendirme aracının ifade kontrolleri aynı kuralı paylaşır.
/// </summary>
/// <remarks>
/// Kullanıcılar sık sık Türkçe karakter kullanmadan yazar ("iade suresi kac gun"), dokümanlar ise doğru yazılmıştır; iki
/// yazım ancak ikisi de aynı ASCII biçime katlanırsa buluşur. Kural tek yerde tutulur ki indeksleme, sorgu ve doğrulama
/// farklı normalleştirmeler uygulayıp birbirini ıskalamasın.
/// </remarks>
public static class TurkishTextNormalizer
{
    /// <summary>Küçük harfe çevirmede kullanılan Türkçe kültür (İ→i, I→ı kuralları için).</summary>
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// Türkçe büyük/küçük harf kurallarıyla küçük harfe çevirir, Türkçe harfleri ASCII'ye katlar (ç→c, ğ→g, ı→i, ö→o, ş→s,
    /// ü→u) ve harf/rakam olmayan her karakter dizisini tek bir boşluğa indirir: "İade süresi kaç gün?" → "iade suresi kac
    /// gun". Kullanıcılar çoğu zaman Türkçe karakter kullanmadan yazdığından iki yazımın aynı biçimde buluşması gerekir.
    /// </summary>
    /// <remarks>
    /// Kültür açıkça <c>tr-TR</c> verilir: sonuç sunucunun kültür ayarına bağlı kalmaz ve "İ"/"I" Türkçe kurallarla eşlenir.
    /// Aksanlar Unicode FormD ayrıştırmasıyla atıldığı için Türkçe dışı aksanlı harfler de katlanır (é→e). Sonuç baştan ve
    /// sondan kırpılmış, tek boşluklu bir dizedir; boş ya da yalnızca boşluk içeren girdi için boş dize döner.
    /// </remarks>
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // FormD, "ş" harfini "s" + birleşen çengel (cedilla), "ü" harfini "u" + iki nokta (diaeresis) olarak ayırır; ç, ğ ve ö
        // için de durum aynıdır. Ayrılan birleşen işaretler aşağıdaki döngüde atılır.
        var decomposed = text.ToLower(Turkish).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // Noktasız ı'nın ayrıştırması yoktur (kendi başına bir harftir), bu yüzden açıkça i'ye katlanır.
            var folded = character == 'ı' ? 'i' : character;

            // Noktalama ve boşluk dizileri tek bir ayraca indirgenir; ayraç ancak başta değilse ve ardından bir harf/rakam
            // gelirse yazılır, böylece sonuç kendiliğinden kırpılmış olur.
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
