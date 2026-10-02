using Knowledge.Infrastructure.Search;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Search;

/// <summary>
/// <see cref="SearchTokenizer"/> birim testleri: Türkçe normalizasyon, durak sözcük (stopword) eleme ve F5 kök alma
/// (sözcüğün ilk 5 karakteri) adımlarını doğrular. Aynı tokenizer hem indekslenen bölüm metnine hem soruya
/// uygulandığından buradaki bir değişiklik BM25 sıralamasını ve Kapı 1'in kapsama (coverage) hesabını birlikte etkiler.
/// </summary>
public sealed class SearchTokenizerTests
{
    /// <summary>
    /// "kaç" ve "içinde" gibi durak sözcüklerin atıldığını, 5 karakterden uzun sözcüklerin ilk 5 karakterine
    /// kısaltıldığını ("edebilirim" → "edebi") ve kısa sözcüklerin olduğu gibi kaldığını doğrular.
    /// </summary>
    /// <remarks>
    /// Türkçe sondan eklemeli bir dildir; F5 kök alma "kargosu", "kargolar" gibi çekimli biçimleri aynı terime
    /// ("kargo") indirger ve Türkçe bilgi erişimi çalışmalarında morfolojik analize yakın sonuç veren basit bir
    /// yöntemdir. Soru sözcükleri ve bağlaçlar konu bilgisi taşımaz; elenmeseler BM25 skorlarını ve kapsama oranını
    /// yapay olarak yükseltir, alan dışı bir sorunun Kapı 1'i geçmesini kolaylaştırırdı.
    /// </remarks>
    [Fact]
    public void Tokenize_drops_stopwords_and_truncates_words_to_five_character_stems()
    {
        SearchTokenizer.Tokenize("Ürünü kaç gün içinde iade edebilirim?")
            .ShouldBe(["urunu", "gun", "iade", "edebi"]);
    }

    /// <summary>
    /// Aynı sorunun Türkçe karakterli ("İade süresi kaç gün?") ve Türkçe karaktersiz ("iade suresi kac gun")
    /// yazımlarının birebir aynı terimleri ürettiğini doğrular; büyük "İ" harfi ve soru işareti de bu örnekle sınanır.
    /// Kullanıcılar sık sık Türkçe karakter kullanmadan yazar; iki yazım aynı terimlere inmeseydi aynı soru yazılışına
    /// göre farklı sonuç alır ya da gereksiz yere reddedilirdi.
    /// </summary>
    [Theory]
    [InlineData("iade suresi kac gun")]
    [InlineData("İade süresi kaç gün?")]
    public void Tokenize_produces_the_same_terms_with_or_without_turkish_characters(string question)
    {
        SearchTokenizer.Tokenize(question).ShouldBe(["iade", "sures", "gun"]);
    }

    /// <summary>
    /// Sayıların tek haneli olsalar bile korunduğunu ("2.4" → "2", "4"), tek harflerin ("E-posta"dan kalan "e")
    /// atıldığını ve noktalama işaretlerinin sözcük ayırıcı sayıldığını doğrular. Destek dokümanlarındaki kritik bilgi
    /// çoğu zaman bir sayıdır (30 gün, 750 TL, 2.4 GHz); tek karakterli token'ları atan kural rakamları da silseydi bu
    /// tür sorular sözcüksel aramada eşleşmezdi.
    /// </summary>
    [Fact]
    public void Tokenize_keeps_numbers_and_drops_single_letters()
    {
        SearchTokenizer.Tokenize("E-posta 2.4 GHz ve 750 TL")
            .ShouldBe(["posta", "2", "4", "ghz", "750", "tl"]);
    }
}
