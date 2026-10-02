using Knowledge.Application.Contracts;
using Shouldly;
using SupportAssistant.Eval;

namespace SupportAssistant.UnitTests.Evaluation;

/// <summary>
/// Değerlendirme aracındaki <see cref="EvalChecks"/> sınıfının birim testleri. Değerlendirme LLM'i hakem olarak kullanmak
/// yerine deterministik kontrollerle yapılır (sonuç tekrarlanabilir; aynı küçük modelle hakemlik zayıf kalırdı). Bu
/// testler o kontrollerin kendisinin doğru çalıştığını, çalışan bir API olmadan elle kurulmuş <c>AnswerDto</c>
/// nesneleriyle güvenceye alır; kontroller hatalı olsaydı 16/16 gibi bir değerlendirme sonucu da anlamsız olurdu.
/// </summary>
public sealed class EvalChecksTests
{
    /// <summary>
    /// Kontrollerin okuduğu alanlar dışında sabit değerler taşıyan bir API yanıtı kurar: yanıt metni, <c>answerable</c>
    /// bayrağı, atıf yapılan doküman kimlikleri (her biri doğrulanmış alıntılı bir kaynak olarak) ve elenen sürüm
    /// kimlikleri. Yanıtlanamayan yanıtlarda ret gerekçesi <c>LowRelevance</c> olarak doldurulur.
    /// </summary>
    private static AnswerDto Answer(string text, bool answerable = true, string[]? sources = null, string[]? discarded = null) => new(
        "soru",
        answerable,
        text,
        (sources ?? []).Select(id => new AnswerSourceDto(id, "Başlık", "1.0", new DateOnly(2025, 1, 1), "active", "politika", "Bölüm", "alıntı", true)).ToList(),
        new VersionResolutionDto(
            (discarded ?? []).Length > 0,
            "kural",
            [],
            (discarded ?? []).Select(id => new DiscardedVersionDto(id, "Başlık", "1.0", new DateOnly(2024, 1, 1), "gerekçe")).ToList()),
        [],
        string.Empty,
        answerable ? string.Empty : "LowRelevance",
        new AnswerDiagnosticsDto("hybrid", 0.7, 1.0, [], [], "model", 100, null, null, 1));

    /// <summary>
    /// İfade eşleştirmesinin büyük/küçük harf, noktalama ve Türkçe karakter farklarını yok saydığını ve Türkçe ekleri
    /// tolere ettiğini ("ücretsiz" → "ücretsizdir"; "2,4 GHz" → "2.4 GHz"), ama ifadenin bir sözcük başından itibaren
    /// bütün olarak geçmesini istediğini doğrular: "30 gün", "300 gün" ile; "ücretsiz", "ücreti alınır" ile eşleşmez.
    /// </summary>
    /// <remarks>
    /// Model aynı bilgiyi farklı ek, harf büyüklüğü, Türkçe karakter ya da ondalık ayırıcıyla yazabilir; katı bir
    /// karşılaştırma doğru yanıtları kaldırırdı. Ek toleransı ise yalnızca ifadenin sonunda geçerlidir: ifade içindeki
    /// "30" sözcüğü "300"ün başı sayılsaydı yanlış süreyi söyleyen bir yanıt geçerdi; arama tarafındaki F5 kökleri
    /// kullanılsaydı zıt anlamlı "ücretsiz" ve "ücreti" aynı köke ("ucret") inip eşleşirdi.
    /// </remarks>
    [Theory]
    [InlineData("Kargo ücretsizdir.", "ücretsiz", true)]
    [InlineData("Kargo ucretsizdir.", "ÜCRETSİZ", true)]
    [InlineData("Ürünü 300 gün içinde iade edin.", "30 gün", false)]
    [InlineData("Kargo ücreti alınır.", "ücretsiz", false)]
    [InlineData("Yalnızca 2.4 GHz ağlar desteklenir.", "2,4 GHz", true)]
    public void Phrases_match_at_word_starts_ignoring_case_punctuation_and_turkish_characters(string text, string phrase, bool expected)
    {
        EvalChecks.ContainsPhrase(text, phrase).ShouldBe(expected);
    }

    /// <summary>
    /// <c>MustContain</c> içindeki her grubun ayrı bir "içerik" kontrolü ürettiğini ve bir grubun, alternatiflerinden biri
    /// yanıtta geçtiğinde başarılı sayıldığını doğrular: ["30 gün"] grubu geçer, ["45 gün", "otuz gün"] grubu kalır.
    /// Gruplar arasında VE, grup içinde VEYA mantığı, beklenen her bilginin ayrı ayrı aranmasını sağlarken aynı bilginin
    /// farklı yazımlarını ("30 gün" / "otuz gün") kabul etmeye izin verir.
    /// </summary>
    [Fact]
    public void Every_must_contain_group_needs_one_of_its_phrases()
    {
        var expect = new EvalExpectation(true, MustContain: [["30 gün"], ["45 gün", "otuz gün"]]);

        var checks = EvalChecks.Evaluate(expect, Answer("Ürünü 30 gün içinde iade edebilirsiniz."));

        checks.Where(check => check.Name == "içerik").Select(check => check.Passed).ShouldBe([true, false]);
    }

    /// <summary>
    /// Doğru kaynağa (<c>iade-v2</c>) atıf yapılmış olsa bile yasak kaynağa (<c>iade-v1</c>) atıf yapılmasının ve eski
    /// kurala ait yasak ifadenin ("14 gün"; yanıtta "14 gündür") kendi kontrollerini ayrı ayrı başarısız kıldığını
    /// doğrular.
    /// </summary>
    /// <remarks>
    /// Çelişkili sürüm sorularında doğru dokümanı da göstermek yeterli değildir: eski sürümün kuralı yanıta karışırsa
    /// sürüm çözümlemesinin önlemesi gereken hata tam olarak gerçekleşmiş olur. Ek toleransı yasak ifadelerde de
    /// geçerlidir; "14 gündür" yazımı kontrolden kaçamaz.
    /// </remarks>
    [Fact]
    public void Forbidden_phrases_and_forbidden_sources_fail_their_checks()
    {
        var expect = new EvalExpectation(true, SourcesAnyOf: ["iade-v2"], ForbiddenSources: ["iade-v1"], MustNotContain: ["14 gün"]);

        var checks = EvalChecks.Evaluate(expect, Answer("İade süresi 14 gündür.", sources: ["iade-v1", "iade-v2"]));

        checks.Single(check => check.Name == "kaynak").Passed.ShouldBeTrue();
        checks.Single(check => check.Name == "yasak kaynak yok").Passed.ShouldBeFalse();
        checks.Single(check => check.Name == "yasak ifade").Passed.ShouldBeFalse();
    }

    /// <summary>
    /// <c>SourcesAllOf</c> beklentisinde listedeki her dokümana atıf yapılması gerektiğini doğrular: yalnızca <c>kargo</c>
    /// atfı başarısız, <c>kurulum</c> ve <c>kargo</c> birlikte (sıra fark etmeksizin) başarılıdır. İki parçalı sorularda
    /// (ör. teslim süresi + Wi-Fi kurulumu) "herhangi biri" kontrolü yanıtın yarısının eksik olmasını gizlerdi.
    /// </summary>
    [Fact]
    public void All_of_sources_must_each_be_cited()
    {
        var expect = new EvalExpectation(true, SourcesAllOf: ["kargo", "kurulum"]);

        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["kargo"])).Single(check => check.Name == "kaynaklar").Passed.ShouldBeFalse();
        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["kurulum", "kargo"])).Single(check => check.Name == "kaynaklar").Passed.ShouldBeTrue();
    }

    /// <summary>
    /// <c>DiscardedVersions</c> beklentisinde eski sürümün (<c>iade-v1</c>) yanıtın <c>versionResolution.discarded</c>
    /// listesinde raporlanması gerektiğini doğrular. Görev, kaynaklar çeliştiğinde güncel sürümün nasıl seçildiğinin
    /// gösterilmesini ister; doğru yanıt verilse bile eleme raporlanmıyorsa bu gereksinim karşılanmamış sayılır.
    /// </summary>
    [Fact]
    public void Outdated_versions_must_be_reported_as_discarded()
    {
        var expect = new EvalExpectation(true, DiscardedVersions: ["iade-v1"]);

        EvalChecks.Evaluate(expect, Answer("30 gün.", discarded: [])).Single(check => check.Name == "eski sürüm elendi").Passed.ShouldBeFalse();
        EvalChecks.Evaluate(expect, Answer("30 gün.", discarded: ["iade-v1"])).Single(check => check.Name == "eski sürüm elendi").Passed.ShouldBeTrue();
    }

    /// <summary>
    /// Yanıtlanmaması gereken bir soruda yalnızca "yanıtlanabilirlik" kontrolünün üretildiğini doğrular: sistem yanıt
    /// verirse kontrol başarısız, reddederse başarılıdır; asla karşılanamayacak bir <c>MustContain</c> beklentisi bile
    /// değerlendirmeye girmez.
    /// </summary>
    /// <remarks>
    /// Yeterli bilgi yoksa bunu açıkça söylemek görevin bir gereksinimidir; bu yüzden ret kendi başına doğru davranıştır.
    /// Bir retin ya da yanlışlıkla verilmiş bir yanıtın içerik kontrolleri anlamlı bir şey söylemez, raporu yalnızca
    /// gürültüyle doldururdu.
    /// </remarks>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void A_question_that_must_be_declined_is_only_checked_for_the_refusal(bool actuallyAnswered, bool expectedPass)
    {
        var expect = new EvalExpectation(false, MustContain: [["olmayacak ifade"]]);

        var checks = EvalChecks.Evaluate(expect, Answer("Bilgi bulunamadı.", answerable: actuallyAnswered));

        checks.ShouldHaveSingleItem().Name.ShouldBe("yanıtlanabilirlik");
        checks[0].Passed.ShouldBe(expectedPass);
    }

    /// <summary>
    /// Arama isabetinin <c>SourcesAnyOf</c> için beklenen kaynaklardan en az birinin, <c>SourcesAllOf</c> için hepsinin
    /// arama sonuçlarında bulunmasını gerektirdiğini ve beklenen kaynağı olmayan (yanıtlanamaz) sorularda <c>null</c>
    /// döndüğünü doğrular.
    /// </summary>
    /// <remarks>
    /// Arama isabeti yanıt üretiminden ayrı olarak sözcüksel ve hibrit modda ölçülür; böylece bir hatanın aramadan mı
    /// üretimden mi kaynaklandığı ayırt edilir. <c>null</c>, yanıtlanamaz soruları isabet oranının paydasından çıkarır;
    /// aksi hâlde bu sorular ıska sayılıp oranı haksız yere düşürürdü.
    /// </remarks>
    [Fact]
    public void Retrieval_hit_needs_one_expected_source_or_all_of_them()
    {
        EvalChecks.RetrievalHit(new EvalExpectation(true, SourcesAnyOf: ["a", "b"]), ["x", "b"]).ShouldBe(true);
        EvalChecks.RetrievalHit(new EvalExpectation(true, SourcesAllOf: ["a", "b"]), ["a", "x"]).ShouldBe(false);
        EvalChecks.RetrievalHit(new EvalExpectation(false), ["a"]).ShouldBeNull();
    }
}
