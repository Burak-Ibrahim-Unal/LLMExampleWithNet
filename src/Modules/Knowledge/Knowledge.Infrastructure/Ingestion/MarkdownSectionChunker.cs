using System.Text;
using System.Text.RegularExpressions;
using Knowledge.Application.Abstractions;

namespace Knowledge.Infrastructure.Ingestion;

/// <summary>
/// Markdown gövdesini her başlık için bir chunk olacak şekilde böler. Her chunk başlık yolunu taşır
/// ("2. Destek Seviyeleri &gt; 2.2 Seviye 2 (L2)"); böylece cevaplar tam olarak hangi bölüme dayandığını gösterebilir.
/// </summary>
/// <remarks>
/// "Bir bölüm = bir chunk" kuralı, her cevabın kullandığı dokümanı ve ilgili bölümü göstermesi şartından gelir: sabit
/// boyutlu pencereler bir kuralı koşulundan ayırabilir ve alıntıyı belirli bir başlığa bağlamayı zorlaştırırdı. Yalnızca
/// çok uzun bölümler (<see cref="DefaultMaxChunkChars"/> üstü) paragraf sınırlarından bölünür; parçalar aynı bölüm yolunu
/// taşır. <c>partial</c> olması, <c>GeneratedRegex</c> kaynak üretecinin regex kodunu bu sınıfa ekleyebilmesi içindir.
/// </remarks>
public static partial class MarkdownSectionChunker
{
    /// <summary>
    /// Bir chunk için hedeflenen üst uzunluk (karakter). Odaklı chunk'lar BM25 ve embedding sıralamasını keskin tutar ve
    /// modele giden bağlamı (TopK = 8 chunk) makul boyutta sınırlar. Mevcut bilgi tabanındaki en uzun bölüm bu sınırın çok
    /// altındadır; sınır, daha uzun dokümanlar eklendiğinde devreye giren bir güvencedir.
    /// </summary>
    public const int DefaultMaxChunkChars = 1200;

    /// <summary>
    /// Gövdeyi satır satır dolaşır: başlık olmayan satırları biriktirir, her yeni başlıkta birikmiş metni o ana kadarki
    /// başlık yoluyla bir bölüm olarak kaydeder ve başlık yığınını günceller.
    /// </summary>
    /// <param name="markdownBody">Front matter'ı ayrılmış Markdown gövdesi.</param>
    /// <param name="documentTitle">İlk alt başlıktan önceki metin için bölüm yolu olarak kullanılan doküman başlığı.</param>
    /// <param name="maxChunkChars">Bu uzunluğu aşan bölümler paragraf sınırlarından bölünür.</param>
    /// <remarks>
    /// Yeni bir başlık geldiğinde aynı veya daha derin seviyedeki başlıklar yığından atılır; böylece "### 2.2" başlığı
    /// "## 2." altına yerleşir, sonraki "## 3." ise yeniden ikinci seviyeden başlar. Sonuçta her bölüm, belgedeki tam
    /// konumunu söyleyen bir yol taşır.
    /// </remarks>
    public static IReadOnlyList<SourceSection> Split(string markdownBody, string documentTitle, int maxChunkChars = DefaultMaxChunkChars)
    {
        var sections = new List<SourceSection>();
        var headings = new List<(int Level, string Text)>();
        var buffer = new StringBuilder();

        // CRLF burada da normalize edilir: Split, ayrıştırıcıdan bağımsız olarak da çağrılabilen public bir API'dir.
        foreach (var line in markdownBody.Replace("\r\n", "\n").Split('\n'))
        {
            var heading = HeadingPattern().Match(line);

            if (!heading.Success)
            {
                buffer.Append(line).Append('\n');
                continue;
            }

            Flush();

            var level = heading.Groups[1].Value.Length;
            headings.RemoveAll(existing => existing.Level >= level);
            headings.Add((level, heading.Groups[2].Value.Trim()));
        }

        Flush();
        return sections;

        // Birikmiş metni o anki başlık yolu altında bir veya (uzunsa) birkaç bölüm olarak kaydeder ve tamponu boşaltır.
        // Yerel fonksiyon olması, sections/headings/buffer durumunu parametre geçirmeden paylaşmasını sağlar.
        void Flush()
        {
            var content = buffer.ToString().Trim();
            buffer.Clear();

            // Yalnızca alt bölümleri gruplayan başlıkların (ör. hemen ardından "### 2.1" gelen "## 2. Seviyeler") kendi metni
            // yoktur; boş chunk üretilmez.
            if (content.Length == 0)
            {
                return;
            }

            // Birinci seviye başlık doküman başlığıdır ve her kaynak gösteriminde zaten yer alır; yola eklemek yalnızca
            // tekrar olurdu. Henüz alt başlık yoksa (ilk "##" öncesindeki giriş metni) yol olarak doküman başlığı kullanılır.
            var pathParts = headings.Where(existing => existing.Level > 1).Select(existing => existing.Text).ToList();
            var path = pathParts.Count > 0 ? string.Join(" > ", pathParts) : documentTitle;

            foreach (var part in SplitAtParagraphs(content, maxChunkChars))
            {
                sections.Add(new SourceSection(path, part));
            }
        }
    }

    /// <summary>
    /// Sınırı aşan bir bölümü boş satırlarla ayrılmış paragraflarına ayırır ve paragrafları sırayla, sınırı aşmayacak
    /// şekilde parçalarda toplar (parça içinde paragraflar arasındaki boş satır korunur). Sınırın altındaki bölüm olduğu
    /// gibi döner.
    /// </summary>
    /// <remarks>
    /// Sınırdan uzun tek bir paragraf bölünmeden bütün olarak tutulur: paragrafın ortasından kesmek bir kuralı koşulundan
    /// ayırabilirdi. Bu yüzden sınır kesin bir üst limit değil, bir hedeftir.
    /// </remarks>
    private static IEnumerable<string> SplitAtParagraphs(string content, int maxChunkChars)
    {
        if (content.Length <= maxChunkChars)
        {
            yield return content;
            yield break;
        }

        var current = new StringBuilder();

        foreach (var paragraph in ParagraphSeparator().Split(content).Select(part => part.Trim()).Where(part => part.Length > 0))
        {
            if (current.Length > 0 && current.Length + 2 + paragraph.Length > maxChunkChars)
            {
                yield return current.ToString();
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append("\n\n");
            }

            current.Append(paragraph);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    /// <summary>
    /// ATX tipi Markdown başlığını yakalar: 1. grup <c>#</c> işaretleridir (seviye 1–6), 2. grup başlık metnidir; sondaki
    /// isteğe bağlı kapanış <c>#</c>'leri ve boşluklar atılır. <c>#</c>'den sonra boşluk zorunludur, bu yüzden
    /// <c>#etiket</c> gibi satırlar başlık sayılmaz. <c>GeneratedRegex</c> ifadeyi derleme zamanında koda çevirir; çalışma
    /// anında regex kurma maliyeti olmaz.
    /// </summary>
    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*#*\s*$")]
    private static partial Regex HeadingPattern();

    /// <summary>
    /// Paragraf ayırıcısı: arasında yalnızca boşluk veya sekme bulunabilen boş satır. Editörlerin bıraktığı görünmez
    /// boşluklar paragraf sınırının kaçırılmasına yol açmaz.
    /// </summary>
    [GeneratedRegex(@"\n[ \t]*\n")]
    private static partial Regex ParagraphSeparator();
}
