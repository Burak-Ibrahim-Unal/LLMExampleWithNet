using Knowledge.Application.Abstractions;
using Knowledge.Application.Text;

namespace Knowledge.Application.Answering;

/// <summary>
/// Kapı 3'ün (atıf doğrulama) yardımcısı; modelin kendi <c>answerable</c> kararından (Kapı 2) sonra çalışır. Yalnızca
/// modele gerçekten verilmiş bir kaynağa işaret eden atıfları tutar ve her alıntının o kaynakta gerçekten geçip geçmediğini
/// denetler. Model, verilen bağlamın dışına atıf yaparak kaçamaz.
/// </summary>
/// <remarks>
/// Kabul kararı etikete dayanır: etiketi bağlamdaki C1..Cn'den biri olmayan atıf düşürülür. Alıntı denetimi atfı
/// düşürmez; sonucu <c>QuoteVerified</c> olarak yanıtta her kaynağın yanında raporlanır. Model <c>answerable=true</c>
/// dediği hâlde kabul edilen hiçbir atıf kalmazsa handler yanıtı <c>NoValidCitations</c> (Kapı 3) ile reddeder. Bu
/// denetimler kodda yapılır, çünkü modelin "kaynağa dayandım" beyanı tek başına kanıt değildir.
/// </remarks>
public static class CitationValidator
{
    /// <summary>Alıntıda kısaltma için kullanılan üç nokta biçimleri: üç ayrı nokta ve tek karakterlik "…".</summary>
    private static readonly string[] Ellipses = ["...", "…"];

    /// <summary>
    /// Model atıflarını bağlamla karşılaştırır: bilinmeyen etiketleri atar, tekrarlanan (etiket, alıntı) çiftlerini
    /// tekilleştirir ve her atıf için alıntının kaynakta geçip geçmediğini işaretler.
    /// </summary>
    /// <remarks>
    /// Etiketler <c>SourceLabel.Normalize</c> ile tek biçime getirilir ("c1", "[C1]", "1" → "C1"), çünkü modeller etiketi
    /// farklı yazabilir; biçim farkı yüzünden geçerli bir atfı kaybetmek gereksiz bir ret üretirdi. Tekilleştirme, aynı
    /// alıntının yanıtın kaynak listesinde iki kez görünmesini önler. Sonuç modelin atıf sırasını korur.
    /// </remarks>
    /// <param name="citations">Modelin döndürdüğü atıflar.</param>
    /// <param name="context">Modele bu istekte verilen etiketli bölümler.</param>
    /// <returns>Kabul edilen atıflar; hiçbiri kabul edilmezse boş liste.</returns>
    public static IReadOnlyList<ValidatedCitation> Validate(IReadOnlyList<GeneratedCitation> citations, IReadOnlyList<ContextChunk> context)
    {
        var sourcesByLabel = context.ToDictionary(source => source.Label, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<(string Label, string Quote)>();
        var validated = new List<ValidatedCitation>();

        foreach (var citation in citations)
        {
            if (!sourcesByLabel.TryGetValue(SourceLabel.Normalize(citation.ChunkLabel), out var source))
            {
                continue;
            }

            var quote = citation.Quote.Trim();

            if (seen.Add((source.Label, quote)))
            {
                validated.Add(new ValidatedCitation(source, quote, IsVerbatim(quote, source.Chunk.Content)));
            }
        }

        return validated;
    }

    /// <summary>
    /// Alıntının kaynak metinde geçip geçmediğini normalleştirilmiş biçimde (büyük/küçük harf, Türkçe harfler ve noktalama
    /// farkı gözetilmeden) denetler; parçalar arasında "..." ile yapılan kısaltmalara izin verilir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Birebir karakter karşılaştırması, modelin noktalama ya da Türkçe karakterde yaptığı zararsız farklar yüzünden doğru
    /// alıntıları da reddederdi; bu yüzden iki taraf da <c>TurkishTextNormalizer</c> ile aynı biçime indirilir.
    /// </para>
    /// <para>
    /// Gevşek bir alt dize araması ise yanlış alıntıları da kabul ederdi; bu yüzden üç kural uygulanır:
    /// <list type="bullet">
    /// <item><description>Üç noktayla ayrılan parçalar kaynakta aynı sırayla geçmelidir; her parça bir öncekinin
    /// bittiği yerden sonra aranır (<c>30...iade</c>, "İade süresi 30 gündür." metninden doğrulanmaz).</description></item>
    /// <item><description>Her parça bir sözcük başında başlamalıdır ("0 gün", "30 gün" içinde eşleşmez).</description></item>
    /// <item><description>Rakamla biten parçanın ardından kaynakta rakam gelmemelidir ("30", "300" içinde eşleşmez).
    /// Sözcük sonu yalnızca rakamlar için denetlenir; Türkçe ekler ("30 gün" ↔ "30 gündür") serbesttir.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Doğrulanmış alıntı yanıtın tek dayanağı olduğundan bu kuralların amacı, kaynağın söylemediği bir şeyin "birebir
    /// alıntı" diye geçmesini önlemektir. Boş ya da yalnızca noktalamadan oluşan bir alıntı doğrulanmış sayılmaz.
    /// </para>
    /// </remarks>
    private static bool IsVerbatim(string quote, string sourceText)
    {
        var fragments = quote
            .Split(Ellipses, StringSplitOptions.RemoveEmptyEntries)
            .Select(TurkishTextNormalizer.Normalize)
            .Where(fragment => fragment.Length > 0)
            .ToList();

        if (fragments.Count == 0)
        {
            return false;
        }

        // Normalleştirilmiş metin tek boşlukla ayrılmış sözcüklerden oluşur; başa eklenen boşluk sayesinde her sözcük başı
        // " " + parça biçiminde aranabilir.
        var source = " " + TurkishTextNormalizer.Normalize(sourceText);
        var position = 0;

        foreach (var fragment in fragments)
        {
            var end = FindAtWordStart(source, fragment, position);

            if (end < 0)
            {
                return false;
            }

            position = end;
        }

        return true;
    }

    /// <summary>
    /// <paramref name="fragment"/> parçasını <paramref name="source"/> içinde <paramref name="start"/> konumundan
    /// itibaren, bir sözcük başında başlayan ilk geçerli yerde arar ve eşleşmenin bittiği konumu döndürür; bulunamazsa -1.
    /// </summary>
    /// <remarks>
    /// Bitiş konumu döndürülür, çünkü çağıran bir sonraki parçayı oradan itibaren arar ve parça sırası böyle korunur.
    /// Rakamla biten bir parçanın kaynakta daha uzun bir sayının başı olduğu eşleşmeler ("30" ↔ "300") atlanır ve arama
    /// sonraki adayla sürer; aynı metinde sayı başka bir yerde tek başına geçiyorsa o eşleşme kabul edilir.
    /// </remarks>
    /// <param name="source">Başına boşluk eklenmiş, normalleştirilmiş kaynak metni.</param>
    /// <param name="fragment">Normalleştirilmiş alıntı parçası (boş değil).</param>
    /// <param name="start">Aramanın başlayacağı konum; önceki parçanın bittiği yer.</param>
    private static int FindAtWordStart(string source, string fragment, int start)
    {
        var needle = " " + fragment;
        var endsWithDigit = char.IsAsciiDigit(fragment[^1]);

        for (var index = source.IndexOf(needle, start, StringComparison.Ordinal);
             index >= 0;
             index = source.IndexOf(needle, index + 1, StringComparison.Ordinal))
        {
            var end = index + needle.Length;

            if (!endsWithDigit || end == source.Length || !char.IsAsciiDigit(source[end]))
            {
                return end;
            }
        }

        return -1;
    }
}

/// <summary>Kabul edilmiş bir atıf: modele verilen kaynak, modelin alıntısı ve alıntının doğrulanma sonucu.</summary>
/// <param name="Source">Atfın işaret ettiği, bağlamdaki kaynak.</param>
/// <param name="Quote">Modelin alıntısı (baştaki ve sondaki boşluklar kırpılmış).</param>
/// <param name="QuoteVerified">Alıntı, atıf yapılan bölümde gerçekten geçiyorsa true (büyük/küçük harf ve Türkçe karakter farkı gözetilmez).</param>
public sealed record ValidatedCitation(ContextChunk Source, string Quote, bool QuoteVerified);
