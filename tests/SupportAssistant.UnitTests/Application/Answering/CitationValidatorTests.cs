using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Domain.Entities;
using Shouldly;

namespace SupportAssistant.UnitTests.Application.Answering;

/// <summary>
/// <see cref="CitationValidator"/> (Kapı 3) için birim testleri. Doğrulayıcı, modelin atıflarından yalnızca kendisine
/// gerçekten verilen bir kaynağa (C1..Cn) işaret edenleri tutar ve her alıntının o bölümde gerçekten geçip geçmediğini
/// Türkçe normalizasyonla (büyük/küçük harf, Türkçe karakterler, noktalama) kontrol eder; kısaltılmış alıntılarda parça
/// sırasını, sözcük başını ve sayı sınırını da denetler.
/// </summary>
/// <remarks>
/// Model çıktısı yerine doğrudan <c>GeneratedCitation</c> listeleri verilir; iki bölümlük sabit bir bağlam (<c>C1</c>:
/// 30 günlük iade süresi, <c>C2</c>: ücretsiz iade kargosu) tüm testlerde ortaktır.
/// </remarks>
public sealed class CitationValidatorTests
{
    /// <summary>
    /// Verilen etiket ve içerikle bir bağlam bölümü (<c>ContextChunk</c>) oluşturur. Doğrulayıcı yalnızca etiketi ve
    /// içeriği kullandığından diğer alanlar sabittir; doküman kimliği etiketten türetilir.
    /// </summary>
    private static ContextChunk Source(string label, string content) =>
        new(label, new IndexedChunk(Guid.NewGuid(), $"doc-{label}", "iade", "İade", "2.0", new DateOnly(2025, 6, 1),
            DocumentStatus.Active, DocumentCategory.Policy, "Bölüm", content));

    /// <summary>
    /// Modele verilmiş sayılan bağlam: <c>C1</c> Türkçe karakterli uzun bir iade cümlesi (normalizasyon ve kısmi alıntı
    /// testleri için), <c>C2</c> kısa bir kargo cümlesi (parafraz ve tekrar testleri için).
    /// </summary>
    private static readonly IReadOnlyList<ContextChunk> Context =
    [
        Source("C1", "Müşteriler, ürünü teslim aldıkları tarihten itibaren 30 gün içinde iade talebinde bulunabilir."),
        Source("C2", "İade kargosu ücretsizdir.")
    ];

    /// <summary>
    /// Bağlamda olmayan bir etikete ("C9", alıntısı "uydurma") yapılan atıfın atıldığını, geçerli <c>C1</c> atıfının ise
    /// korunduğunu doğrular.
    /// </summary>
    /// <remarks>
    /// Model kendisine verilmemiş bir kaynağı gösteremez; aksi hâlde uydurulmuş bir etiket yanıtı kaynaklıymış gibi
    /// gösterirdi. Bütün atıflar bu şekilde düşerse handler modeli bir kez düzeltme talimatıyla yeniden çağırır, yine
    /// olmazsa yanıtı <c>NoValidCitations</c> ile reddeder.
    /// </remarks>
    [Fact]
    public void Citations_to_sources_that_were_not_provided_are_dropped()
    {
        var validated = CitationValidator.Validate([new("C9", "uydurma"), new("C1", "30 gün içinde iade talebinde bulunabilir")], Context);

        validated.ShouldHaveSingleItem().Source.Label.ShouldBe("C1");
    }

    /// <summary>
    /// Kaynakta gerçekten geçen bir alıntının etiket ve yazım farklılıklarına rağmen doğrulandığını
    /// (<c>QuoteVerified=true</c>) gösterir: etiket "C1", "c1" ya da "[C1]" biçiminde gelebilir; alıntı büyük harfle ve
    /// Türkçe karakter kullanılmadan ("30 GUN ICINDE IADE TALEBINDE") yazılmış ya da cümlenin bir parçası olabilir.
    /// </summary>
    /// <remarks>
    /// Küçük modeller etiket biçimini tutarsız üretir ve zaman zaman Türkçe karakterleri ya da harf büyüklüğünü değiştirir.
    /// Katı bir karşılaştırma doğru alıntıları "doğrulanmamış" sayar; etiket uyuşmazlığı ise geçerli atıfları düşürüp
    /// gereksiz retlere yol açardı.
    /// </remarks>
    [Theory]
    [InlineData("C1", "30 gün içinde iade talebinde bulunabilir")]
    [InlineData("c1", "30 GUN ICINDE IADE TALEBINDE")]
    [InlineData("[C1]", "teslim aldıkları tarihten itibaren 30 gün")]
    public void A_quote_found_in_the_source_is_verified_regardless_of_case_or_turkish_characters(string label, string quote)
    {
        CitationValidator.Validate([new(label, quote)], Context).ShouldHaveSingleItem().QuoteVerified.ShouldBeTrue();
    }

    /// <summary>
    /// Kaynağı doğru gösteren ama metni birebir aktarmayan bir alıntının ("iade kargosu bedava"; kaynakta "ücretsizdir")
    /// doğrulayıcıda atılmadığını, yalnızca <c>QuoteVerified=false</c> olarak işaretlendiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Doğrulayıcı karar vermez, ölçer: atfın doğru bölüme işaret ettiği ama alıntının doğrulanamadığı bilgisi handler'a
    /// kadar taşınır. Handler doğrulanmamış atıfları yanıtın kaynaklarına koymaz; hiç doğrulanmış atıf kalmazsa modeli bir
    /// kez düzeltme talimatıyla yeniden çağırır ve doğrulanamayan alıntıları bu talimatta modele gösterir. Atıf burada
    /// atılsaydı handler hangi alıntının düzeltilmesi gerektiğini bilemezdi.
    /// </remarks>
    [Fact]
    public void A_paraphrased_quote_keeps_its_source_but_is_marked_unverified()
    {
        var citation = CitationValidator.Validate([new("C2", "iade kargosu bedava")], Context).ShouldHaveSingleItem();

        citation.Source.Label.ShouldBe("C2");
        citation.QuoteVerified.ShouldBeFalse();
    }

    /// <summary>
    /// Üç noktayla kısaltılmış bir alıntının yalnızca parçaları kaynakta aynı sırayla geçiyorsa doğrulandığını gösterir:
    /// "30 gün … talebinde" kaynaktaki sırayı korur ve doğrulanır; "talebinde … 30 gün" aynı sözcükleri ters sırayla
    /// birleştirir ve doğrulanmaz.
    /// </summary>
    /// <remarks>
    /// Parçalar sırasız aransaydı, kaynaktaki sözcüklerden yeni bir anlam kurmak mümkün olurdu: "İade süresi 30 gündür."
    /// metninden <c>30...iade</c> gibi kaynağın söylemediği bir bağlantı "birebir alıntı" diye geçerdi. Doğrulanmış alıntı
    /// artık yanıtın tek dayanağı olduğu için kısaltma da kaynağın sırasını korumak zorundadır.
    /// </remarks>
    [Theory]
    [InlineData("30 gün … talebinde", true)]
    [InlineData("30 gün... iade talebinde bulunabilir", true)]
    [InlineData("talebinde … 30 gün", false)]
    [InlineData("iade...30", false)]
    public void Quote_fragments_must_appear_in_the_source_in_order(string quote, bool verified)
    {
        CitationValidator.Validate([new("C1", quote)], Context).ShouldHaveSingleItem().QuoteVerified.ShouldBe(verified);
    }

    /// <summary>
    /// Alıntının kaynakta bir sözcük başından başlaması gerektiğini ve rakamla biten bir alıntının kaynakta daha uzun bir
    /// sayının parçası olarak eşleşmediğini doğrular: "300 gün" içeren bir kaynakta "30" ve "0 gün" doğrulanmaz;
    /// "300 gün" ve Türkçe ek almış "300 gündür" metnindeki "300 gün" doğrulanır.
    /// </summary>
    /// <remarks>
    /// Düz alt dize araması, "30 gün" diyen bir yanıtı "300 gün" yazan bir kaynakla destekleniyormuş gibi gösterirdi;
    /// süre, tutar ve eşik gibi sayılar destek yanıtlarının en kritik bilgisidir. Sözcük sonu ise yalnızca rakamlar için
    /// denetlenir: Türkçe ekler ("gün" → "gündür") doğru bir alıntıyı doğrulanmamış saymamalıdır.
    /// </remarks>
    [Theory]
    [InlineData("30", false)]
    [InlineData("0 gün", false)]
    [InlineData("300 gün", true)]
    [InlineData("Teslim süresi 300", true)]
    public void Quotes_match_at_word_starts_and_numbers_do_not_match_inside_longer_numbers(string quote, bool verified)
    {
        IReadOnlyList<ContextChunk> context = [Source("C3", "Teslim süresi 300 gündür.")];

        CitationValidator.Validate([new("C3", quote)], context).ShouldHaveSingleItem().QuoteVerified.ShouldBe(verified);
    }

    /// <summary>
    /// Aynı etiket ve aynı alıntıyla iki kez yapılan atıfın tek bir atıfa indirildiğini doğrular. Modeller aynı kaynağı
    /// art arda tekrarlayabilir; yinelenen kayıtlar yanıttaki <c>sources</c> listesini gereksiz yere şişirirdi.
    /// </summary>
    [Fact]
    public void Repeated_citations_are_collapsed()
    {
        CitationValidator.Validate([new("C2", "İade kargosu ücretsizdir."), new("C2", "İade kargosu ücretsizdir.")], Context)
            .ShouldHaveSingleItem();
    }
}
