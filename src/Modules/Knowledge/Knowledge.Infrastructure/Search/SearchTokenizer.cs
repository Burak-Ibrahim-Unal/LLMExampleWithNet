using Knowledge.Application.Text;

namespace Knowledge.Infrastructure.Search;

/// <summary>
/// Metni arama terimlerine çevirir: Türkçe normalizasyon, stopword (etkisiz kelime) temizliği ve sabit önekli kök bulma
/// (stemming). Hem indekslenen chunk'lar hem de sorgular aynı fonksiyondan geçer; iki taraf ancak böyle aynı terim
/// biçiminde buluşur.
/// </summary>
/// <remarks>
/// Türkçe eklemeli (agglutinative) bir dildir ("iade", "iadesi", "iadeler"): aynı kök birçok yüzey biçiminde geçer. İlk
/// beş karakteri tutmak (F5), Türkçe bilgi erişimi (IR) çalışmalarında morfolojik analizle yarışabilir bulunmuş basit bir
/// kök bulma yöntemidir ve sözlük ya da morfolojik çözümleyici bağımlılığı getirmez. Kökü en az beş karakter olan
/// kelimelerde ekli biçimler tek terimde birleşir ("ücreti", "ücretsiz" → "ucret"; "teslim", "teslimat" → "tesli").
/// Daha kısa köklerde ekin ilk harfi terime girer ("iade" → "iade" ama "iadesi" → "iades"); bu bilinen sınırlamayı
/// hibrit moddaki embedding araması kısmen telafi eder.
/// </remarks>
public static class SearchTokenizer
{
    /// <summary>F5 kök uzunluğu: bir kelimenin terim olarak tutulan ilk karakter sayısı.</summary>
    public const int StemLength = 5;

    /// <summary>
    /// Sık geçen işlev kelimelerinin (bağlaç, edat, zamir) ve soru eklerinin katlanmış (ASCII) biçimleri. Normalizasyondan
    /// sonra karşılaştırıldıkları için "için" ve "icin" aynı girdiyle elenir.
    /// </summary>
    /// <remarks>
    /// Soru kelimeleri ("kaç", "nasıl", "nedir") dokümanlarda nadiren geçer; tutulsalardı yüksek idf ağırlığı alıp
    /// kapsama (coverage) oranını düşürür ve Kapı 1'de haksız retlere yol açarlardı. Bağlaçlar ise hemen her chunk'ta
    /// geçtiğinden yalnızca gürültü eklerdi.
    /// </remarks>
    private static readonly HashSet<string> Stopwords =
    [
        "ve", "veya", "ile", "ya", "bir", "bu", "su", "o", "da", "de", "ki", "mi", "mu", "ne",
        "icin", "icinde", "gibi", "daha", "en", "cok", "ama", "ancak", "fakat", "ise", "hem", "her",
        "nasil", "neden", "hangi", "kac", "olan", "olarak", "var", "yok", "ben", "sen", "biz", "siz",
        "benim", "bana", "beni", "sizin", "size", "sizi", "miyim", "misin", "misiniz", "musunuz",
        "midir", "mudur", "nedir", "nerede", "nereden", "kadar", "sonra", "once", "eger", "yani", "gore"
    ];

    /// <summary>
    /// Metni normalize eder (<c>TurkishTextNormalizer</c>: tr-TR kurallarıyla küçük harf, ç ğ ı ö ş ü → c g i o s u,
    /// harf ve rakam dışındaki karakterler boşluk), stopword'leri ve tek harfli parçaları atar, kalan kelimeleri F5 köküne
    /// kısaltır. Terimler metindeki sırasıyla ve tekrarlarıyla döner; tekrarları BM25 terim frekansı olarak kullanır.
    /// </summary>
    /// <remarks>
    /// Kullanıcılar sıkça Türkçe karakter kullanmadan yazar ("iade suresi kac gun"); normalizasyon iki yazımı aynı
    /// terimlere indirger. Tek harfler ("E-posta" → "e") anlam taşımadığı için atılır, ama tek haneli rakamlar korunur:
    /// "2.4 GHz" veya "5 iş günü" gibi sayılar destek sorularında belirleyicidir.
    /// </remarks>
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
