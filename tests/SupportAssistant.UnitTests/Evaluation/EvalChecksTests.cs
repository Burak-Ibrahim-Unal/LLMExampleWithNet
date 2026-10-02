using Knowledge.Application.Contracts;
using Shared.Application.Common;
using Shouldly;
using SupportAssistant.Eval;
using static SupportAssistant.UnitTests.Evaluation.EvalAnswers;

namespace SupportAssistant.UnitTests.Evaluation;

/// <summary>
/// Değerlendirme aracındaki <see cref="EvalChecks"/> sınıfının birim testleri. Değerlendirme LLM'i hakem olarak kullanmak
/// yerine deterministik kontrollerle yapılır (sonuç tekrarlanabilir; aynı küçük modelle hakemlik zayıf kalırdı). Bu
/// testler o kontrollerin kendisinin doğru çalıştığını, çalışan bir API olmadan elle kurulmuş <c>AnswerDto</c>
/// nesneleriyle güvenceye alır; kontroller hatalı olsaydı 16/16 gibi bir değerlendirme sonucu da anlamsız olurdu.
/// </summary>
/// <remarks>
/// Testlerin bir kısmı bilerek yanlış cevaplarla kurulur ("doğru kaynak + yanlış karar", "doğru alıntı + yanlış sayı"):
/// bir değerlendiricinin güvenilirliği, doğru cevapları geçirmesi kadar yanlış cevapları kaldırmasına da bağlıdır.
/// </remarks>
public sealed class EvalChecksTests
{
    /// <summary>Doküman metni gerektirmeyen testler için boş doküman sözlüğü.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoDocuments = new Dictionary<string, string>();

    /// <summary>
    /// Sayı kontrolü testlerinin kullandığı küçük doküman metinleri: güncel iade politikası (30 gün) ve kargo
    /// dokümanı (750 TL eşiği, 49,90 TL ücret). Gerçek koşuda bu metinler API'nin doküman uçlarından alınır.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Documents = new Dictionary<string, string>
    {
        ["iade-v2"] = "İade Politikası 2.0 2025-06-01 2. İade Süresi Ürünü teslim aldıktan sonra 30 gün içinde iade edebilirsiniz.",
        ["kargo"] = "Kargo ve Teslimat 1.0 2025-03-01 2. Kargo Ücreti 750 TL ve üzerindeki siparişlerde kargo ücretsizdir. " +
            "750 TL'nin altındaki siparişler için 49,90 TL kargo ücreti alınır."
    };

    /// <summary>Bir çelişki kaynağını yalnızca doküman kimliğiyle kurar; kontrol diğer alanlara bakmaz.</summary>
    private static ConflictSourceDto ConflictSource(string documentId) =>
        new(documentId, "1.0", new DateOnly(2025, 1, 1), "politika", "Bölüm");

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

        var checks = EvalChecks.Evaluate(expect, Answer("Ürünü 30 gün içinde iade edebilirsiniz."), "soru", NoDocuments);

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

        var checks = EvalChecks.Evaluate(expect, Answer("İade süresi 14 gündür.", sources: ["iade-v1", "iade-v2"]), "soru", NoDocuments);

        checks.Single(check => check.Name == "kaynak").Passed.ShouldBeTrue();
        checks.Single(check => check.Name == "yasak kaynak yok").Passed.ShouldBeFalse();
        checks.Single(check => check.Name == "yasak ifade").Passed.ShouldBeFalse();
    }

    /// <summary>
    /// Arkadaş incelemesinin kabul ölçütü: "750 TL üzerindeki siparişlerde kargo ücretsiz değildir; 999 TL alınır."
    /// yanıtı doğru dokümana atıf yapsa ve beklenen sayıyı (750) içerse bile N04 beklentisinden geçmez. Ters karar
    /// "ücretsiz değil" yasak ifadesine, uydurma sayı (999) ise kaynağa dayalı sayı kontrolüne takılır.
    /// </summary>
    /// <remarks>
    /// Eski N04 beklentisi yalnızca "750" arıyordu; anahtar kelime ve kaynak kontrolü yanlış bir kararı yakalayamıyordu.
    /// Bu test, sıkılaştırılmış beklentinin ve sayı kontrolünün o açığı gerçekten kapattığını gösterir.
    /// </remarks>
    [Fact]
    public void A_wrong_decision_with_the_right_number_and_source_fails()
    {
        var expect = new EvalExpectation(true, SourcesAnyOf: ["kargo"], MustContain: [["750"], ["ücretsiz"]], MustNotContain: ["ücretsiz değil"]);

        var checks = EvalChecks.Evaluate(
            expect,
            Answer("750 TL üzerindeki siparişlerde kargo ücretsiz değildir; 999 TL alınır.", sources: ["kargo"]),
            "Kaç TL ve üzeri siparişlerde kargo ücretsiz oluyor?",
            Documents);

        checks.Single(check => check.Name == "kaynak").Passed.ShouldBeTrue();
        checks.Single(check => check.Name == "yasak ifade").Passed.ShouldBeFalse();
        var numbers = checks.Single(check => check.Name == "sayılar kaynakta");
        numbers.Passed.ShouldBeFalse();
        numbers.Detail.ShouldContain("999");
    }

    /// <summary>
    /// Yanıttaki her sayının atıf yapılan dokümanların metninde ya da sorunun kendisinde geçmesi gerektiğini doğrular:
    /// "30 gün" kaynakta olduğu için geçer; doğru kaynağı gösteren ama "14 gün" diyen yanıt kalır; sorudan gelen 749 TL
    /// ile kaynaktaki 49,90 TL'yi birlikte kullanan yanıt geçer.
    /// </summary>
    /// <remarks>
    /// "Doğru alıntı + yanlış sayı" anahtar kelime kontrolüyle yakalanamaz, çünkü her soru için olası tüm yanlış sayılar
    /// önceden yazılamaz. Sayıları kaynağa dayandırmak genel bir ölçüttür: uydurma bir süre, tutar ya da eşik hangi soruda
    /// olursa olsun görünür olur, soruda istenmeyen ama kaynakta geçen doğru bir ayrıntı ise cezalandırılmaz.
    /// </remarks>
    [Theory]
    [InlineData("İade süresi 30 gündür.", "iade-v2", true)]
    [InlineData("İade süresi 14 gündür.", "iade-v2", false)]
    [InlineData("749 TL'lik siparişte 49,90 TL kargo ücreti alınır.", "kargo", true)]
    public void Numbers_must_come_from_the_cited_documents_or_the_question(string text, string source, bool expected)
    {
        var checks = EvalChecks.Evaluate(new EvalExpectation(true), Answer(text, sources: [source]), "749 TL'lik siparişte kargo ücretli mi?", Documents);

        checks.Single(check => check.Name == "sayılar kaynakta").Passed.ShouldBe(expected);
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

        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["kargo"]), "soru", NoDocuments).Single(check => check.Name == "kaynaklar").Passed.ShouldBeFalse();
        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["kurulum", "kargo"]), "soru", NoDocuments).Single(check => check.Name == "kaynaklar").Passed.ShouldBeTrue();
    }

    /// <summary>
    /// <c>SectionsAnyOf</c> beklentisinde atıf yapılan bölümlerden birinin beklenen bölüm adını içermesi gerektiğini
    /// doğrular: "2. Kargo Ücreti" bölümü "Kargo Ücreti" beklentisini karşılar, aynı dokümanın "1. Teslimat Süresi"
    /// bölümü karşılamaz.
    /// </summary>
    /// <remarks>
    /// Doküman kontrolü, doğru dokümanın yanlış bölümüne dayanan bir yanıtı geçirirdi; oysa görev her yanıtın kullanılan
    /// bölümü göstermesini istiyor. Bölüm adı ifade eşleştirmesiyle karşılaştırılır, numara ve noktalama farkı önemsizdir.
    /// </remarks>
    [Fact]
    public void The_expected_section_must_be_cited()
    {
        var expect = new EvalExpectation(true, SectionsAnyOf: ["Kargo Ücreti"]);

        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["kargo"], section: "2. Kargo Ücreti"), "soru", NoDocuments)
            .Single(check => check.Name == "bölüm").Passed.ShouldBeTrue();
        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["kargo"], section: "1. Teslimat Süresi"), "soru", NoDocuments)
            .Single(check => check.Name == "bölüm").Passed.ShouldBeFalse();
    }

    /// <summary>
    /// Yanıtlanan bir soruda her kaynağın alıntısının doğrulanmış olması gerektiğini doğrular; doğrulanmamış bir kaynak
    /// "alıntı doğrulandı" kontrolünü düşürür.
    /// </summary>
    /// <remarks>
    /// API yalnızca doğrulanmış alıntılı kaynakları listeler; bu kontrol o sözleşmeyi uçtan uca, istemci tarafından da
    /// denetler. Sözleşme bir gün bozulursa değerlendirme bunu sessizce geçmez.
    /// </remarks>
    [Fact]
    public void Every_source_must_have_a_verified_quote()
    {
        var expect = new EvalExpectation(true);

        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["kargo"]), "soru", NoDocuments)
            .Single(check => check.Name == "alıntı doğrulandı").Passed.ShouldBeTrue();
        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["kargo"], quoteVerified: false), "soru", NoDocuments)
            .Single(check => check.Name == "alıntı doğrulandı").Passed.ShouldBeFalse();
    }

    /// <summary>
    /// <c>ExpectConflict</c> beklentisinin, seçilen ve elenen dokümanları doğru olan ve kurala uygun (<c>ruleSatisfied</c>)
    /// bir çelişki kaydı istediğini doğrular: doğru kayıt geçer; kayıt yoksa ya da seçim tersse kontrol kalır.
    /// </summary>
    /// <remarks>
    /// C04 sorusunun amacı kaynaklar arası çelişkinin görünür ve doğru çözülmesidir; yalnızca doğru cevabı aramak, modelin
    /// çelişkiyi hiç fark etmediği bir yanıtı da başarılı sayardı.
    /// </remarks>
    [Fact]
    public void An_expected_conflict_must_be_reported_with_the_rule_satisfied()
    {
        var expect = new EvalExpectation(true, ExpectConflict: new ExpectedConflict("iade-v2", ["sss"]));

        // Yerel yardımcı: seçilen ve elenen dokümanı ile kurala uygunluğu verilen tek bir çelişki kaydı kurar; üç senaryo
        // yalnızca bu üç değerde ayrıştığı için tekrar eden DTO kurulumunu tek satıra indirir.
        ConflictDto Conflict(string chosen, string rejected, bool satisfied) =>
            new("İade kargo ücreti", ConflictSource(chosen), [ConflictSource(rejected)], "gerekçe", satisfied);

        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["iade-v2"], conflicts: [Conflict("iade-v2", "sss", true)]), "soru", NoDocuments)
            .Single(check => check.Name == "çelişki kaydı").Passed.ShouldBeTrue();
        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["iade-v2"]), "soru", NoDocuments)
            .Single(check => check.Name == "çelişki kaydı").Passed.ShouldBeFalse();
        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["iade-v2"], conflicts: [Conflict("sss", "iade-v2", false)]), "soru", NoDocuments)
            .Single(check => check.Name == "çelişki kaydı").Passed.ShouldBeFalse();
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

        EvalChecks.Evaluate(expect, Answer("30 gün.", discarded: []), "soru", NoDocuments).Single(check => check.Name == "eski sürüm elendi").Passed.ShouldBeFalse();
        EvalChecks.Evaluate(expect, Answer("30 gün.", discarded: ["iade-v1"]), "soru", NoDocuments).Single(check => check.Name == "eski sürüm elendi").Passed.ShouldBeTrue();
    }

    /// <summary>
    /// Yanıtlanmaması gereken bir soruda içerik kontrollerinin çalışmadığını doğrular: sistem yanıt verirse yalnızca
    /// başarısız "yanıtlanabilirlik" kontrolü üretilir; reddederse yanıtlanabilirlik ve ret sözleşmesi kontrolleri üretilir
    /// ve ikisi de geçer. Asla karşılanamayacak bir <c>MustContain</c> beklentisi bile değerlendirmeye girmez.
    /// </summary>
    /// <remarks>
    /// Yeterli bilgi yoksa bunu açıkça söylemek görevin bir gereksinimidir; bu yüzden ret kendi başına doğru davranıştır.
    /// Bir retin ya da yanlışlıkla verilmiş bir yanıtın içerik kontrolleri anlamlı bir şey söylemez, raporu yalnızca
    /// gürültüyle doldururdu.
    /// </remarks>
    [Theory]
    [InlineData(true, new[] { "yanıtlanabilirlik" }, false)]
    [InlineData(false, new[] { "yanıtlanabilirlik", "ret sözleşmesi" }, true)]
    public void A_question_that_must_be_declined_is_only_checked_for_the_refusal(bool actuallyAnswered, string[] expectedChecks, bool expectedPass)
    {
        var expect = new EvalExpectation(false, MustContain: [["olmayacak ifade"]]);

        var checks = EvalChecks.Evaluate(expect, Answer(Messages.Knowledge.NotEnoughInformation, answerable: actuallyAnswered), "soru", NoDocuments);

        checks.Select(check => check.Name).ShouldBe(expectedChecks);
        checks.All(check => check.Passed).ShouldBe(expectedPass);
    }

    /// <summary>
    /// Cevapsız bir soruda reddin API sözleşmesine uyması gerektiğini doğrular: gerekçeye ait sabit mesaj ("yeterli bilgi
    /// bulunamadı"; prompt injection ve çıktı koruması retlerinde onlara özel mesaj), boş kaynak listesi ve dolu bir ret
    /// gerekçesi. Kaynak listeleyen, gerekçesine uymayan bir metin döndüren ya da gerekçesiz bir ret "ret sözleşmesi"
    /// kontrolünü düşürür.
    /// </summary>
    /// <remarks>
    /// "Bilgi yok" yanıtının biçimi istemcilerin dayandığı bir sözleşmedir: kaynak gösteren bir ret, kullanıcıya bir
    /// yanıtın dayanağı varmış izlenimi verir; gerekçesiz bir ret ise hangi kapıda durulduğunu gizler. Sorun dokümanlarda
    /// değil sorunun kendisinde ya da modelin çıktısında olduğunda API bilerek farklı bir mesaj döndürür; değerlendirici bu
    /// retleri sözleşme ihlali saymamalı, ama gerekçeyle mesajın eşleşmesini yine denetlemelidir.
    /// </remarks>
    /// <param name="text">
    /// Ret metni: null ise "bilgi yok" mesajı, bir <see cref="Messages.Knowledge"/> özelliğinin adı ise o mesaj, değilse
    /// olduğu gibi kullanılan serbest metin. Mesajlar dosyadan okunduğu için niteliğe değeri değil adı yazılır.
    /// </param>
    /// <param name="source">Retle birlikte listelenen kaynak; null ise kaynak listesi boştur.</param>
    /// <param name="reason">Retteki <c>refusalReason</c>.</param>
    /// <param name="expected">"ret sözleşmesi" kontrolünün geçip geçmemesi.</param>
    [Theory]
    [InlineData(null, null, "LowRelevance", true)]
    [InlineData(null, "kargo", "LowRelevance", false)]
    [InlineData("Bilmiyorum.", null, "LowRelevance", false)]
    [InlineData(null, null, "", false)]
    [InlineData(nameof(Messages.Knowledge.PromptInjectionRefused), null, "PromptInjectionSuspected", true)]
    [InlineData(nameof(Messages.Knowledge.UnsafeOutputRefused), null, "UnsafeOutput", true)]
    [InlineData(null, null, "PromptInjectionSuspected", false)]
    [InlineData(nameof(Messages.Knowledge.UnsafeOutputRefused), null, "LowRelevance", false)]
    public void A_refusal_must_use_the_fixed_message_list_no_sources_and_give_a_reason(string? text, string? source, string reason, bool expected)
    {
        var message = text switch
        {
            null => Messages.Knowledge.NotEnoughInformation,
            nameof(Messages.Knowledge.PromptInjectionRefused) => Messages.Knowledge.PromptInjectionRefused,
            nameof(Messages.Knowledge.UnsafeOutputRefused) => Messages.Knowledge.UnsafeOutputRefused,
            _ => text
        };
        var answer = Answer(message, answerable: false, sources: source is null ? null : [source], refusalReason: reason);

        EvalChecks.Evaluate(new EvalExpectation(false), answer, "soru", NoDocuments)
            .Single(check => check.Name == "ret sözleşmesi").Passed.ShouldBe(expected);
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

    /// <summary>
    /// <c>Conditions</c> gruplarının "içerik"ten ayrı, "koşul" adıyla raporlandığını doğrular: eşiği doğru yönde söyleyen
    /// yanıt koşulu geçer; eşiği tersine çeviren yanıt sayıyı içerdiği için içerik kontrolünü geçer ama koşul kontrolünde
    /// kalır.
    /// </summary>
    /// <remarks>
    /// Sayının varlığı ile doğru kullanımı ayrı kontrollerdir; raporda ikisinin ayrı görünmesi, bir yanıtın neden kaldığını
    /// ("sayı var ama koşul ters") açıkça gösterir.
    /// </remarks>
    [Fact]
    public void Conditions_are_reported_separately_from_content()
    {
        var expect = new EvalExpectation(true, MustContain: [["750"]], Conditions: [["750 TL ve üzer", "en az 750"]]);

        var right = EvalChecks.Evaluate(expect, Answer("750 TL ve üzeri siparişlerde kargo ücretsizdir."), "soru", NoDocuments);
        var reversed = EvalChecks.Evaluate(expect, Answer("750 TL altındaki siparişlerde kargo ücretsizdir."), "soru", NoDocuments);

        right.Single(check => check.Name == "koşul").Passed.ShouldBeTrue();
        reversed.Single(check => check.Name == "içerik").Passed.ShouldBeTrue();
        reversed.Single(check => check.Name == "koşul").Passed.ShouldBeFalse();
    }
}
