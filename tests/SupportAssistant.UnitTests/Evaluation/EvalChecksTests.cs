using System.Text.Json;
using Knowledge.Application.Contracts;
using Shared.Application.Common;
using Shouldly;
using SupportAssistant.Eval;

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

    /// <summary>
    /// Kontrollerin okuduğu alanlar dışında sabit değerler taşıyan bir API yanıtı kurar: yanıt metni, <c>answerable</c>
    /// bayrağı, atıf yapılan doküman kimlikleri (aynı bölüm adı ve alıntı doğrulama durumuyla), elenen sürüm kimlikleri,
    /// çelişkiler ve ret gerekçesi. Ret gerekçesi verilmezse yanıtlanamayan yanıtlarda <c>LowRelevance</c> kullanılır.
    /// </summary>
    private static AnswerDto Answer(
        string text,
        bool answerable = true,
        string[]? sources = null,
        string[]? discarded = null,
        string section = "Bölüm",
        bool quoteVerified = true,
        IReadOnlyList<ConflictDto>? conflicts = null,
        string? refusalReason = null) => new(
        "soru",
        answerable,
        text,
        (sources ?? []).Select(id => new AnswerSourceDto(id, "Başlık", "1.0", new DateOnly(2025, 1, 1), "active", "politika", section, "alıntı", quoteVerified)).ToList(),
        new VersionResolutionDto(
            (discarded ?? []).Length > 0,
            "kural",
            [],
            (discarded ?? []).Select(id => new DiscardedVersionDto(id, "Başlık", "1.0", new DateOnly(2024, 1, 1), "gerekçe")).ToList()),
        conflicts ?? [],
        string.Empty,
        refusalReason ?? (answerable ? string.Empty : "LowRelevance"),
        new AnswerDiagnosticsDto("hybrid", 0.7, 1.0, [], [], "model", 100, null, null, 1));

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

    /// <summary>
    /// Soru dosyalarındaki gerçek beklentilerin (<c>eval/questions*.json</c>) kritik bir kararın koşulunu tersine çeviren,
    /// olumsuzlayan, eski kuralı söyleyen ya da sorudaki yanlış öncülü kabul eden yanıtları kaldırdığını doğrular. İlk satır
    /// arkadaş incelemesinin örneğidir: doğru kaynak, doğru bölüm, doğrulanmış alıntı ve kaynakta geçen sayıyla "750 TL
    /// altındaki siparişlerde kargo ücretsizdir" diyen yanıt önceki beklentilerin hepsinden geçiyordu.
    /// </summary>
    /// <remarks>
    /// Beklentiler veri dosyasından okunur; dosyadaki bir koşul grubu ya da yasak ifade silinirse bu test kırılır. Her
    /// yanıt beklenen dokümana ve bölüme atıf yapar ve sayıları kaynakta ya da soruda geçer; kalma nedeni yalnızca içerik,
    /// koşul ya da yasak ifade kontrolleridir. Bağımsız setlerin ve halüsinasyon setinin satırları, beklentiler yazılırken
    /// ve ilk koşudan önce eklendi; koşu sonuçlarına göre değiştirilmez. Kontroller ifade tabanlı olduğu için kısmidir:
    /// buradaki örnekler yakalanır, ama her ters anlatım yakalanmaz; değerlendirme raporundaki yanıtlar bu yüzden ayrıca
    /// elle okunur.
    /// </remarks>
    [Theory]
    [InlineData("N04", "750 TL altındaki siparişlerde kargo ücretsizdir.")]
    [InlineData("N04", "Kargo, 750 TL'nin altındaki siparişlerde ücretsizdir.")]
    [InlineData("N04", "Kargo yalnızca 750 TL'den az tutarlı siparişlerde ücretsizdir.")]
    [InlineData("N04", "750 TL ve üzerindeki siparişlerde kargo ücretsiz değildir.")]
    [InlineData("N04", "750 TL ve üzerindeki siparişlerde kargo ücretsiz sayılmaz; kargo ücreti alınır.")]
    [InlineData("C01", "Ürünü teslim aldıktan 30 gün sonra iade edebilirsiniz.")]
    [InlineData("C02", "Ücret, ürün depoya ulaştıktan 5 iş günü sonra hesabınıza geçer.")]
    [InlineData("N08", "Ürün depoya ulaştıktan 5 iş gününden sonra paranız iade edilir.")]
    [InlineData("N03", "Su hasarı garanti kapsamı dışında değildir; cihazınız ücretsiz onarılır.")]
    [InlineData("N03", "Hayır, endişelenmeyin: sıvı teması garanti kapsamındadır.")]
    [InlineData("N10", "Onarım, cihaz servise ulaştıktan 20 iş günü sonra başlar.")]
    [InlineData("N12", "Hasarlı ürünü teslimattan itibaren 3 gün sonra bildirebilirsiniz.")]
    [InlineData("C05", "Çağrı merkezimiz hafta sonları ve resmî tatillerde hizmet vermez; hafta içi ulaşabilirsiniz.")]
    [InlineData("C07", "İade kodunuz talebiniz incelendikten sonra en geç 2 iş günü içinde e-posta ile iletilir.")]
    [InlineData("H23", "Hayır, iade süresi geçtiği için iade talebiniz kabul edilmez.")]
    [InlineData("HL07", "Evet, garanti süresi 3 yıl olduğu için cihazınız hâlâ garanti kapsamındadır.")]
    [InlineData("HL08", "Evet, 500 TL üzeri siparişlerde kargo ücretsiz olduğu için 600 TL'lik siparişinizde kargo ücreti alınmaz.")]
    [InlineData("HL09", "Evet, bir Lumora hesabına 15 cihaz ekleyebilirsiniz.")]
    [InlineData("HL10", "Evet, 14 gün kuralı hâlâ geçerlidir; ürünü 14 gün içinde iade edebilirsiniz.")]
    [InlineData("HL11", "L2'ye aktarılan talepler 24 saat içinde sonuçlandırılır.")]
    [InlineData("HL12", "Evet, seri numarasını uygulamadaki Cihaz Bilgileri ekranından alarak başvurabilirsiniz; arızanız garanti kapsamında onarılır.")]
    public void Reversed_or_negated_conditions_fail_the_real_expectations(string id, string answer)
    {
        var question = RealQuestion(id);

        var checks = EvalChecks.Evaluate(question.Expect, CitingExpected(question, answer), question.Question, KnowledgeBaseTexts.Value);

        checks.Where(check => check.Name is "kaynak" or "bölüm" or "sayılar kaynakta").ShouldAllBe(check => check.Passed);
        checks.Where(check => check.Name is "içerik" or "koşul" or "yasak ifade").ShouldContain(check => !check.Passed);
    }

    /// <summary>
    /// Kayıtlı canlı koşulardaki (varsayılan ve düşünme modu açık) gerçek, doğru yanıtların sıkılaştırılmış beklentilerin
    /// bütün kontrollerinden geçtiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Yeni kontroller yanlış yanıtları kaldırırken doğru yanıtları da kaldırsaydı değerlendirme bu kez ters yönde
    /// yanıltıcı olurdu. Yanıt metinleri <c>eval/results</c> altındaki raporlardan aynen alındı.
    /// </remarks>
    [Theory]
    [InlineData("N03", "Maalesef, sıvı teması, nem veya su hasarı gibi durumlar garanti kapsamı dışında yer almaktadır.")]
    [InlineData("N03", "Hayır, sıvı teması, nem veya su hasarı gibi durumlar garanti kapsamı dışındadır.")]
    [InlineData("N04", "750 TL ve üzerindeki siparişlerde kargo ücretsizdir.")]
    [InlineData("N08", "İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir.")]
    [InlineData("C01", "Ürünü, teslim aldığınız tarihten itibaren 30 gün içinde iade edebilirsiniz. Bu süre, kargo firmasının teslimat kaydındaki tarih esas alınarak hesaplanmaktadır.")]
    [InlineData("C01", "Ürünü teslim aldığınız tarihten itibaren 30 gün içinde iade talebinde bulunabilirsiniz. Ancak, ürün hasarlı veya eksik ise teslimattan itibaren 3 gün içinde bildirimde bulunmanız gerekmektedir.")]
    [InlineData("C02", "İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir.")]
    public void Recorded_correct_answers_pass_the_real_expectations(string id, string answer)
    {
        var question = RealQuestion(id);

        var checks = EvalChecks.Evaluate(question.Expect, CitingExpected(question, answer), question.Question, KnowledgeBaseTexts.Value);

        checks.ShouldAllBe(check => check.Passed);
    }

    /// <summary>
    /// Kayıtlı koşularda görülmemiş ama doğru olan yazımların da gerçek beklentilerden geçtiğini doğrular: "750 TL üstü",
    /// "750 TL veya üzeri", eşiğin altındaki siparişler için doğru bir olumsuzlama ("ücretsiz kargo uygulanmaz"),
    /// sorunun kendi kalıbıyla verilen "5 iş gününde" ve yeni soruların makul doğru yanıtları. Yanlış bir öncülü düzelten
    /// yanıtlar öncülü tekrar eder ("garanti süresi 3 yıl değil, 2 yıldır"); bu tekrar yasak ifadeye takılmamalıdır.
    /// </summary>
    /// <remarks>
    /// Kod incelemesi, ilk koşul ve yasak ifade listelerinin bu doğru yanıtları kaldırdığını gösterdi; kayıtlı koşular
    /// yalnızca model bilgi tabanının ifadesini kopyaladığı için geçiyordu. Liste genişletildi ve ters yazımları yakalayan
    /// yasak ifadeler eşiğe bağlı biçimlere daraltıldı; ters yazımlar yine kalır (yukarıdaki test). Türkçede olumsuzluk
    /// çoğu zaman ayrı bir "değil" sözcüğüyle kurulduğu ve ifade eşleşmesi sözcük sonunu açık bıraktığı için yeni setlerin
    /// yasak ifadeleri olumlu eklerle biter ("…ücretsizdir", "…geçerlidir"); "ücretsiz değildir" bunlarla eşleşmez.
    /// </remarks>
    [Theory]
    [InlineData("N04", "Kargo, 750 TL üstü siparişlerde ücretsizdir.")]
    [InlineData("N04", "750 TL veya üzeri siparişlerde kargo ücretsizdir.")]
    [InlineData("N04", "750 TL ve üzeri siparişlerde kargo ücretsizdir; 750 TL altındaki siparişlerde ücretsiz kargo uygulanmaz.")]
    [InlineData("C02", "İade edilen ürün depoya ulaşıp kontrol edildikten sonra paranız 5 iş gününde hesabınıza geçer.")]
    [InlineData("N08", "Ürün depoya ulaşıp kontrol edildikten sonra ücret 5 iş gününde kartınıza iade edilir.")]
    [InlineData("N09", "Garanti başvurusunu Lumora uygulamasında Destek > Garanti Talebi menüsünden yapabilirsiniz; cihazın seri numarası ve fatura gerekir.")]
    [InlineData("N10", "Garanti kapsamındaki onarım, cihazın servise ulaşmasından itibaren en geç 20 iş günü içinde tamamlanır.")]
    [InlineData("N12", "Hasarlı ürünü teslimattan itibaren 3 gün içinde fotoğraflarla birlikte uygulamadaki Destek > Teslimat Sorunu menüsünden bildirin; ürün ücretsiz olarak değiştirilir.")]
    [InlineData("N14", "Kombiden gelen kabloları montaj plakasındaki COM ve NO uçlarına bağlayın.")]
    [InlineData("C05", "Çağrı merkezimize hafta sonu ve resmî tatiller dahil her gün 08:00–22:00 saatleri arasında ulaşabilirsiniz.")]
    [InlineData("C07", "İade kodu, talebi oluşturduğunuz anda uygulamada görüntülenir.")]
    [InlineData("H14", "Cihaz yazılımı her gece 03:00'te otomatik olarak güncellenir; güncelleme sırasında cihazın elektriğini kesmeyin.")]
    [InlineData("H22", "Evet, çağrı merkezi hafta sonu dahil her gün 08:00–22:00 arasında hizmet verir.")]
    [InlineData("H23", "Evet, ürünü teslim aldığınız tarihten itibaren 30 gün içinde iade edebilirsiniz.")]
    [InlineData("HL07", "Hayır. Garanti süresi 3 yıl değil, fatura tarihinden itibaren 2 yıldır; 2,5 yıl önce aldığınız cihazın garantisi dolmuştur.")]
    [InlineData("HL08", "Hayır, ödersiniz: kargo 750 TL ve üzerindeki siparişlerde ücretsizdir; 600 TL'lik siparişiniz için 49,90 TL kargo ücreti alınır.")]
    [InlineData("HL09", "Hayır, bir Lumora hesabına en fazla 10 cihaz eklenebilir; 15 cihaz eklenemez.")]
    [InlineData("HL10", "Hayır, 14 günlük süre artık geçerli değil; güncel politikaya göre ürünü teslim aldığınız tarihten itibaren 30 gün içinde iade edebilirsiniz.")]
    [InlineData("HL11", "L2 talepleri en geç 2 iş günü içinde sonuçlandırılır; L2 ayrıca 24 saat içinde müşteriye dönüş yapar.")]
    [InlineData("HL12", "Hayır. Seri numarası etiketi okunamaz hâle gelmiş cihazlar garanti kapsamı dışındadır.")]
    public void Plausible_correct_phrasings_pass_the_real_expectations(string id, string answer)
    {
        var question = RealQuestion(id);

        var checks = EvalChecks.Evaluate(question.Expect, CitingExpected(question, answer), question.Question, KnowledgeBaseTexts.Value);

        checks.ShouldAllBe(check => check.Passed);
    }

    /// <summary>
    /// Depodaki bütün soru dosyalarının (ana set, bağımsız setler, halüsinasyon seti) bilgi tabanıyla tutarlı olduğunu
    /// doğrular: en az dört set var; kimlikler bütün setlerde benzersiz; kategoriler raporun tanıdığı üç anahtardan biri;
    /// anılan doküman kimlikleri bilgi tabanında var; beklenen her bölüm adı beklenen kaynaklardan birinin bir başlığında
    /// geçiyor; cevapsız sorular hiçbir içerik beklentisi taşımıyor.
    /// </summary>
    /// <remarks>
    /// Bu hataların hiçbiri değerlendirmede kendini göstermezdi: yanlış yazılmış bir kategori özet tablosundan sessizce
    /// düşer, var olmayan bir doküman kimliği ya da bölüm adı soruyu her koşuda kaldırır, cevapsız bir sorudaki içerik
    /// beklentisi hiç değerlendirilmez. Dosyalar klasörden okunduğu için yeni bir set eklendiğinde test onu da kendiliğinden
    /// kapsar. Bağımsız setler ve halüsinasyon seti bu testten ilk koşudan önce geçer; koşu sonuçlarına göre değiştirilmez.
    /// </remarks>
    [Fact]
    public void Every_question_file_is_consistent_with_the_knowledge_base()
    {
        var questions = QuestionFiles.Value.SelectMany(file => Load(file).Questions.Select(question => (File: Path.GetFileName(file), Question: question))).ToList();

        QuestionFiles.Value.Length.ShouldBeGreaterThanOrEqualTo(4);
        questions.Select(entry => entry.Question.Id).ShouldBeUnique();

        foreach (var (file, question) in questions)
        {
            var label = $"{file} {question.Id}";
            var expect = question.Expect;

            new[] { "normal", "cevapsiz", "celiskili" }.ShouldContain(question.Category, label);

            if (!expect.Answerable)
            {
                expect.ShouldBe(new EvalExpectation(false), label);
                continue;
            }

            var sources = (expect.SourcesAnyOf ?? []).Concat(expect.SourcesAllOf ?? []).ToList();
            IEnumerable<string> conflict = expect.ExpectConflict is { } expected ? [expected.Chosen, .. expected.Rejected] : [];

            sources.ShouldNotBeEmpty(label);
            sources.Concat(expect.ForbiddenSources ?? []).Concat(expect.DiscardedVersions ?? []).Concat(conflict)
                .ShouldAllBe(id => KnowledgeBaseTexts.Value.ContainsKey(id), label);

            foreach (var section in expect.SectionsAnyOf ?? [])
            {
                sources.Any(id => Headings(id).Any(heading => EvalChecks.ContainsPhrase(heading, section))).ShouldBeTrue($"{label}: '{section}'");
            }
        }
    }

    /// <summary>
    /// Depodaki soru dosyaları (<c>eval/questions*.json</c>); depo kökü çalışma klasöründen yukarı doğru, değerlendirme
    /// aracının kullandığı yolla bulunur.
    /// </summary>
    private static readonly Lazy<string[]> QuestionFiles = new(() =>
        Directory.GetFiles(EvalOptions.ResolveFromRepository("eval"), "questions*.json"));

    /// <summary>
    /// Bütün soru dosyalarındaki sorular, kimliğe göre. Kimlikler setler arasında benzersizdir
    /// (<see cref="Every_question_file_is_consistent_with_the_knowledge_base"/>); bir test hangi setteki soruyu kullandığını
    /// yalnızca kimlikle söyler.
    /// </summary>
    private static readonly Lazy<IReadOnlyDictionary<string, EvalQuestion>> RealQuestions = new(() =>
        QuestionFiles.Value.SelectMany(file => Load(file).Questions).ToDictionary(question => question.Id, StringComparer.Ordinal));

    /// <summary>
    /// Gerçek bilgi tabanının doküman metinleri (dosya adı = doküman kimliği). Canlı koşuda bu metinler API'nin doküman
    /// uçlarından alınır; burada aynı dosyalar doğrudan okunur.
    /// </summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> KnowledgeBaseTexts = new(() =>
        Directory.GetFiles(EvalOptions.ResolveFromRepository("knowledge-base"), "*.md")
            .ToDictionary(file => Path.GetFileNameWithoutExtension(file), file => File.ReadAllText(file)));

    /// <summary>Bir soru dosyasını değerlendirme aracının kullandığı JSON ayarlarıyla okur.</summary>
    private static EvalSuite Load(string path) =>
        JsonSerializer.Deserialize<EvalSuite>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    /// <summary>Bir bilgi tabanı dokümanının Markdown başlık satırları (<c>#</c> ile başlayanlar); bölüm adları bunlardır.</summary>
    private static IEnumerable<string> Headings(string documentId) =>
        KnowledgeBaseTexts.Value[documentId].Split('\n').Where(line => line.TrimStart().StartsWith('#'));

    /// <summary>Bütün soru dosyalarından kimliği verilen soruyu döndürür.</summary>
    private static EvalQuestion RealQuestion(string id) => RealQuestions.Value[id];

    /// <summary>
    /// Verilen metinle, sorunun beklediği ilk dokümana ve ilk bölüme atıf yapan ve beklenen eski sürümleri elenmiş olarak
    /// raporlayan bir yanıt kurar; böylece yapısal kontroller geçer ve sonuç yalnızca metnin içeriğine bağlı kalır.
    /// </summary>
    private static AnswerDto CitingExpected(EvalQuestion question, string text) =>
        Answer(
            text,
            sources: [(question.Expect.SourcesAnyOf ?? question.Expect.SourcesAllOf)![0]],
            discarded: question.Expect.DiscardedVersions?.ToArray(),
            section: question.Expect.SectionsAnyOf![0]);
}
