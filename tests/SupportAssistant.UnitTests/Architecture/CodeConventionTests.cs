using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using SupportAssistant.Eval;

namespace SupportAssistant.UnitTests.Architecture;

/// <summary>
/// Kaynak koda konan iki kural: hiçbir C# dosyası <see cref="MaxLines"/> satırı aşmaz ve ürün kodunda (<c>src/</c>)
/// kullanıcıya dönük Türkçe metin bulunmaz; metinler kaynak dosyalarındadır.
/// </summary>
/// <remarks>
/// <para>
/// Satır sınırı, bir sınıfın tek bir işe odaklı kalması içindir; sınırı aşan bir dosya bölünmesi gereken bir sorumluluğa
/// işaret eder. Dosya satırları sayılır (belge yorumları dahil); belge yorumu yoğun bir kod tabanında bu, kod satırından
/// daha katı ama ölçmesi kesin bir ölçüttür.
/// </para>
/// <para>
/// Türkçe metin kuralı Roslyn ile denetlenir: dosyalar ayrıştırılır ve yalnızca string literalleri (normal, verbatim,
/// raw ve interpolated metin parçaları) taranır; yorumlar ve belge yorumları taranmaz. API mesajları
/// <c>Shared.Application/Common/Resources/messages.tr.json</c>'da, dil modeline giden metinler
/// <c>Knowledge.Infrastructure/Llm/Prompts/answer-prompt.tr.yaml</c>'da durur; kodda yalnızca anahtar adları kalır.
/// Bilinçli istisna <c>[GeneratedRegex]</c> kalıplarıdır: onlar mesaj değil algılama kuralıdır (ör. Türkçe prompt
/// injection kalıpları) ve kaynak üreticisi derleme zamanında sabit ister. İngilizce log şablonları ve programcı
/// hatalarına ait istisna mesajları Türkçe değildir ve kullanıcıya dönmez; kuralın dışındadır.
/// </para>
/// <para>
/// Türkçe metin Türkçe harflerinden tanınır; yalnızca ASCII harflerle yazılmış Türkçe bir mesaj ("bilinmeyen status")
/// bu testten kaçabilir, bu yüzden kural kod incelemesinde de aranır. Arama durak sözcükleri gibi veri listeleri mesaj
/// değildir ve kodda kalır.
/// </para>
/// </remarks>
public sealed class CodeConventionTests
{
    /// <summary>Bir C# dosyasının en fazla satır sayısı.</summary>
    private const int MaxLines = 500;

    /// <summary>Kullanıcıya dönük metnin işareti sayılan Türkçe harfler.</summary>
    private const string TurkishLetters = "çğıöşüÇĞİÖŞÜ";

    /// <summary>
    /// <c>src/</c>, <c>tools/</c> ve <c>tests/</c> altındaki hiçbir C# dosyasının <see cref="MaxLines"/> satırı aşmadığını
    /// doğrular; aşan dosyalar satır sayılarıyla listelenir.
    /// </summary>
    [Fact]
    public void No_source_file_exceeds_the_line_limit()
    {
        var tooLong = SourceFiles("src", "tools", "tests")
            .Select(file => (File: RelativePath(file), Lines: File.ReadAllLines(file).Length))
            .Where(entry => entry.Lines > MaxLines)
            .Select(entry => $"{entry.File}: {entry.Lines} satır")
            .ToList();

        tooLong.ShouldBeEmpty();
    }

    /// <summary>
    /// Ürün kodundaki (<c>src/</c>) hiçbir string literalinin Türkçe harf içermediğini doğrular; <c>[GeneratedRegex]</c>
    /// kalıpları hariç. İhlaller dosya, satır ve metinle listelenir.
    /// </summary>
    [Fact]
    public void User_facing_turkish_text_lives_in_resource_files()
    {
        var offenders = SourceFiles("src")
            .SelectMany(file => TurkishLiterals(file).Select(literal => $"{RelativePath(file)}:{literal.Line}: {literal.Text}"))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    /// <summary>
    /// Bir dosyadaki Türkçe harf içeren string literallerini satır numaralarıyla döndürür; <c>[GeneratedRegex]</c>
    /// niteliğinin argümanları atlanır.
    /// </summary>
    private static IEnumerable<(int Line, string Text)> TurkishLiterals(string file)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();

        return root.DescendantTokens()
            .Where(token => token.Kind() is SyntaxKind.StringLiteralToken
                or SyntaxKind.InterpolatedStringTextToken
                or SyntaxKind.SingleLineRawStringLiteralToken
                or SyntaxKind.MultiLineRawStringLiteralToken)
            .Where(token => token.ValueText.IndexOfAny(TurkishLetters.ToCharArray()) >= 0)
            .Where(token => !IsRegexPattern(token))
            .Select(token => (token.GetLocation().GetLineSpan().StartLinePosition.Line + 1, token.ValueText.Trim()));
    }

    /// <summary>Token bir <c>[GeneratedRegex(...)]</c> niteliğinin argümanındaysa true döndürür.</summary>
    private static bool IsRegexPattern(SyntaxToken token) =>
        token.Parent?.AncestorsAndSelf().OfType<AttributeSyntax>().Any(attribute => attribute.Name.ToString() is "GeneratedRegex" or "GeneratedRegexAttribute") == true;

    /// <summary>
    /// Verilen depo klasörlerindeki C# dosyaları; derleme çıktıları (<c>bin</c>, <c>obj</c>) hariç. Depo kökü, değerlendirme
    /// aracının kullandığı yolla (<see cref="EvalOptions.ResolveFromRepository"/>) bulunur.
    /// </summary>
    private static IEnumerable<string> SourceFiles(params string[] folders) =>
        folders
            .SelectMany(folder => Directory.EnumerateFiles(EvalOptions.ResolveFromRepository(folder), "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj"));

    /// <summary>Dosya yolunu depo köküne göre yazar; hata mesajı kısa ve makineden bağımsız kalır.</summary>
    private static string RelativePath(string file) =>
        Path.GetRelativePath(EvalOptions.ResolveFromRepository("."), file).Replace('\\', '/');
}
