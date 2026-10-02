using Knowledge.Application.Text;
using Shouldly;

namespace SupportAssistant.UnitTests.Application.Text;

/// <summary>
/// <see cref="TurkishTextNormalizer"/> için birim testleri. Normalleştirici BM25 tokenizer'ı (<c>SearchTokenizer</c>),
/// alıntı doğrulaması (<c>CitationValidator</c>) ve değerlendirme aracının ifade kontrolleri tarafından ortak kullanılır;
/// buradaki bir hata aramayı, alıntı doğrulamasını ve değerlendirmeyi aynı anda bozar.
/// </summary>
public sealed class TurkishTextNormalizerTests
{
    /// <summary>
    /// Normalleştirmenin Türkçe kurallarla küçük harfe çevirdiğini, Türkçe harfleri ASCII karşılıklarına katladığını ve
    /// noktalamayı boşluğa çevirdiğini doğrular. Satırlar sırasıyla şunları kapsar: noktalı büyük "İ" ve karışık harf
    /// büyüklüğü; noktasız büyük "I" ile "Ş" ("IŞIK" → "isik"); tipik bir soru cümlesi ve soru işareti; kesme işaretiyle
    /// ayrılmış ek, tire ve ondalık nokta ("Wi-Fi'ye 2.4 GHz" → "wi fi ye 2 4 ghz"; rakamlar korunur); art arda boşluk,
    /// satır sonu ve sekmenin tek boşluğa inmesi ve baştan/sondan kırpılma.
    /// </summary>
    /// <remarks>
    /// Kullanıcılar soruları çoğu zaman Türkçe karakter kullanmadan yazar ("iade suresi kac gun"); iki yazım ancak aynı
    /// biçime indirgenirse eşleşir. Küçültme <c>tr-TR</c> kültürüyle yapılır, çünkü Türkçede "I"nın küçüğü "ı", "İ"nin
    /// küçüğü "i"dir; kültür açıkça verildiği için sonuç sunucunun geçerli kültüründen de bağımsızdır.
    /// </remarks>
    [Theory]
    [InlineData("İADE Süresi", "iade suresi")]
    [InlineData("IŞIK", "isik")]
    [InlineData("Garanti süresi kaç gün?", "garanti suresi kac gun")]
    [InlineData("Wi-Fi'ye 2.4 GHz", "wi fi ye 2 4 ghz")]
    [InlineData("  çok   boşluk\n\tvar  ", "cok bosluk var")]
    public void Normalize_lowercases_folds_turkish_letters_and_strips_punctuation(string input, string expected)
    {
        TurkishTextNormalizer.Normalize(input).ShouldBe(expected);
    }
}
