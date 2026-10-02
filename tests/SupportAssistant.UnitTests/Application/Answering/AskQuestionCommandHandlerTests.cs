using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Commands.AskQuestion;
using Knowledge.Application.Contracts;
using Knowledge.Application.Exceptions;
using Knowledge.Application.Options;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Persistence;
using Knowledge.Infrastructure.Search;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Application.Common;
using Shared.Infrastructure.Persistence;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Answering;

/// <summary>
/// Yanıt hattının tamamı (<see cref="AskQuestionCommandHandler"/>) gerçek indeks ve gerçek politikalarla çalıştırılır;
/// yalnızca dil modeli taklit edilir (<see cref="FakeAnswerGenerator"/>). Bellek içi <c>KnowledgeIndex</c>, iş kuralları,
/// <c>AnswerabilityPolicy</c>, <c>VersionResolver</c>, <c>CitationValidator</c>, <c>AnswerText</c> ve SQLite üzerindeki
/// <c>QuestionLogRepository</c> üretimdeki hâlleriyle kullanılır.
/// </summary>
/// <remarks>
/// <para>
/// Böylece Kapı 1, sürüm çözümleme, Kapı 2–3, çelişki denetimi ve ret yanıtlarının biçimi gerçek bir LLM sunucusu
/// olmadan, hızlı ve her çalıştırmada aynı sonucu veren testlerle korunur. İndeks devre dışı bir embedder ile kurulur;
/// yani yalnızca BM25 (lexical) modunda çalışır ve ağa hiç çıkmaz. "Bugün" <see cref="FixedTimeProvider"/> ile
/// 2026-10-01'e sabitlenir.
/// </para>
/// <para>
/// Fixture bilgi tabanı gerçek senaryonun küçültülmüş bir kopyasıdır: iade politikasının eski (1.0, superseded, 14 gün,
/// iade kargosu müşteriye ait) ve güncel (2.0, active, 30 gün, iade kargosu ücretsiz) sürümleri, tek sürümlü bir kargo
/// politikası ve güncel iade politikasıyla çelişen eski tarihli bir SSS. xUnit her test için sınıfın yeni bir örneğini
/// oluşturduğundan fake'in ayarları ve bellek içi veritabanı testler arasında paylaşılmaz.
/// </para>
/// </remarks>
public sealed class AskQuestionCommandHandlerTests : IAsyncLifetime
{
    /// <summary>
    /// Bellek içi SQLite bağlantısı. <c>:memory:</c> veritabanı yalnızca onu açan bağlantı açık kaldığı sürece yaşar; bu
    /// yüzden bağlantı test boyunca açık tutulur ve her <c>AppDbContext</c> aynı bağlantıyı paylaşır.
    /// </summary>
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    /// <summary>
    /// Dil modelinin yerine geçen fake. Testler davranışını <c>Respond</c>/<c>Failure</c> ile belirler; modelin çağrılıp
    /// çağrılmadığını ve neyi gördüğünü <c>Calls</c>/<c>LastContext</c> ile denetler.
    /// </summary>
    private readonly FakeAnswerGenerator _generator = new();
    /// <summary>
    /// Fixture dokümanlarıyla kurulan gerçek arama indeksi. Embedder devre dışı (<c>enabled: false</c>) olduğundan
    /// yalnızca BM25 ile çalışır: sıralama deterministiktir ve hiçbir embedding sunucusuna gerek yoktur.
    /// </summary>
    private readonly KnowledgeIndex _index = new(new FakeTextEmbedder(enabled: false), Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);

    /// <summary>
    /// Verilen bölümlerle bir doküman sürümü (<c>KnowledgeDocument</c>) oluşturur. Başlık ve içerik özeti (hash) kimlikten
    /// türetilir; testler yalnızca sürüm çözümlemeyi ve kaynak önceliğini etkileyen alanları (aile anahtarı, sürüm,
    /// yürürlük tarihi, durum, tür) açıkça yazar.
    /// </summary>
    private static KnowledgeDocument Document(string id, string key, string version, DateOnly effectiveDate, DocumentStatus status, DocumentCategory category, params (string Path, string Content)[] sections)
    {
        var document = new KnowledgeDocument(id, key, $"Belge {key}", version, effectiveDate, status, category, null, $"hash-{id}");

        foreach (var section in sections)
        {
            document.AddChunk(section.Path, section.Content);
        }

        return document;
    }

    /// <summary>
    /// Bağlantıyı açar, şemayı <c>EnsureCreatedAsync</c> ile oluşturur (soru logu tablosu için gerekir; testte migration
    /// çalıştırmaya gerek yoktur) ve indeksi fixture dokümanlarıyla kurar. İndeks veritabanından değil doğrudan bellekteki
    /// dokümanlardan beslenir; bu sınıfta veritabanı yalnızca <c>QuestionLog</c> kayıtları için kullanılır.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        _index.Rebuild(
        [
            Document("iade-v1", "iade", "1.0", new DateOnly(2024, 1, 15), DocumentStatus.Superseded, DocumentCategory.Policy,
                ("2. İade Süresi", "Ürünü teslim aldıktan sonra 14 gün içinde iade edebilirsiniz."),
                ("5. İade Kargo Ücreti", "İade kargo ücreti müşteriye aittir.")),
            Document("iade-v2", "iade", "2.0", new DateOnly(2025, 6, 1), DocumentStatus.Active, DocumentCategory.Policy,
                ("2. İade Süresi", "Ürünü teslim aldıktan sonra 30 gün içinde iade edebilirsiniz."),
                ("5. İade Kargo Ücreti", "İade kargosu ücretsizdir.")),
            Document("kargo", "kargo", "1.0", new DateOnly(2025, 3, 1), DocumentStatus.Active, DocumentCategory.Policy,
                ("2. Kargo Ücreti", "750 TL ve üzeri siparişlerde kargo ücretsizdir.")),
            Document("sss", "sss", "1.0", new DateOnly(2024, 2, 1), DocumentStatus.Active, DocumentCategory.Faq,
                ("İade > İade kargo ücretini kim öder?", "İade kargo ücreti müşteriye aittir."))
        ]);
    }

    /// <summary>
    /// Bağlantıyı kapatır; bellek içi veritabanı da onunla birlikte yok olur. Böylece bir testin yazdığı soru logları
    /// diğerine taşınmaz.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Paylaşılan bellek içi bağlantı üzerinde yeni bir <c>AppDbContext</c> oluşturur. Knowledge modülünün EF
    /// yapılandırmaları üretimdeki gibi <c>EntityConfigurationAssemblyRegistry</c> üzerinden eklenir; böylece testteki şema
    /// gerçek şemayla aynıdır.
    /// </summary>
    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options,
        new EntityConfigurationAssemblyRegistry([Knowledge.Infrastructure.AssemblyReference.Assembly]));

    /// <summary>
    /// Bir soruyu, üretimdeki bağımlılıklarla kurulmuş yeni bir <c>AskQuestionCommandHandler</c> üzerinden sorar; her çağrı
    /// kendi <c>DbContext</c>'ini kullanır (ayrı bir HTTP isteği gibi). Yalnızca üretici sahtedir ve "bugün"
    /// 2026-10-01'e sabitlenir.
    /// </summary>
    /// <param name="question">Sorulacak soru.</param>
    /// <param name="index">
    /// İsteğe bağlı farklı bir indeks; boş ya da yalnızca eski doküman içeren bir indeksle senaryo kuran testler içindir.
    /// İş kuralları da aynı indeksle kurulur ki "indeks hazır mı" kontrolü doğru indeksi denetlesin.
    /// </param>
    private async Task<ApiResult<AnswerDto>> AskAsync(string question, IKnowledgeIndex? index = null)
    {
        await using var context = CreateContext();
        var options = Options.Create(new RetrievalOptions());
        var usedIndex = index ?? _index;
        var handler = new AskQuestionCommandHandler(
            usedIndex,
            _generator,
            new KnowledgeBusinessRules(usedIndex),
            new AnswerabilityPolicy(options),
            new VersionResolver(new FixedTimeProvider(new DateOnly(2026, 10, 1))),
            new QuestionLogRepository(context),
            options,
            NullLogger<AskQuestionCommandHandler>.Instance);

        return await handler.Handle(new AskQuestionCommand(question), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Handler'ın modele verdiği bağlamda belirli bir doküman bölümüne atanmış etiketi ("C1".."Cn") bulur. Etiketler arama
    /// sıralamasına göre atandığından testler sabit etiket yazmak yerine bu yardımcıyı kullanır; sıralama değişse bile
    /// test yanlış bölüme işaret etmez.
    /// </summary>
    private static string LabelOf(IReadOnlyList<ContextChunk> context, string documentId, string section) =>
        context.Single(source => source.Chunk.DocumentId == documentId && source.Chunk.SectionPath == section).Label;

    /// <summary>
    /// Bilgi tabanıyla hiç sözcük paylaşmayan alan dışı bir sorunun ("Apple HomeKit ile uyumlu mu?") Kapı 1'de
    /// <c>LowRelevance</c> gerekçesiyle reddedildiğini ve dil modelinin hiç çağrılmadığını (<c>Calls == 0</c>) doğrular.
    /// Ret bir hata değil geçerli bir iş sonucudur: HTTP 200, <c>answerable=false</c>, sabit "yeterli bilgi bulunamadı"
    /// mesajı ve boş <c>sources</c>.
    /// </summary>
    /// <remarks>
    /// Bu davranış hem gereksiz LLM maliyetini önler hem de alan dışı sorularda modelin "yardımsever" bir uydurma yapma
    /// riskini ortadan kaldırır. Test kırılırsa ya model gereksiz yere çağrılıyordur ya da ret yanıtının sözleşmesi (durum
    /// kodu, gerekçe, sabit mesaj, boş kaynak listesi) bozulmuştur.
    /// </remarks>
    [Fact]
    public async Task An_unrelated_question_is_refused_without_calling_the_model()
    {
        var result = await AskAsync("Apple HomeKit ile uyumlu mu?");

        result.StatusCode.ShouldBe(200);
        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.LowRelevance);
        result.Data.Answer.ShouldBe(Messages.Knowledge.NotEnoughInformation);
        result.Data.Sources.ShouldBeEmpty();
        _generator.Calls.ShouldBe(0);
    }

    /// <summary>
    /// "İade süresi kaç gün?" sorusunda arama hem eski (1.0, 14 gün) hem güncel (2.0, 30 gün) iade politikasını bulur. Test,
    /// modele giden bağlamda (<c>LastContext</c>) eski sürümün hiç bulunmadığını, güncel sürümün bulunduğunu ve kararın
    /// <c>versionResolution</c> içinde raporlandığını doğrular: seçilen <c>iade-v2</c>, elenen <c>iade-v1</c> ve Türkçe
    /// gerekçe "2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.". Varsayılan fake yanıtı <c>iade-v2</c>'ye atıf yaptığı
    /// için iade ailesinin kararı yanıtta görünür.
    /// </summary>
    /// <remarks>
    /// Görevin temel gereksinimlerinden biri budur: çelişen sürümlerde güncel olanın nasıl seçildiği gösterilmelidir. Karar
    /// prompt'a bırakılmadan kodda verildiği için bu test kırılırsa eski iade politikası (14 gün, kargo müşteriye ait)
    /// modele ulaşır ve yanıta karışabilir.
    /// </remarks>
    [Fact]
    public async Task Superseded_versions_are_kept_out_of_the_model_context_and_reported()
    {
        var result = await AskAsync("İade süresi kaç gün?");

        _generator.LastContext.ShouldNotBeEmpty();
        _generator.LastContext.ShouldAllBe(source => source.Chunk.DocumentId != "iade-v1");
        _generator.LastContext.ShouldContain(source => source.Chunk.DocumentId == "iade-v2");

        var resolution = result.Data!.VersionResolution;
        resolution.Applied.ShouldBeTrue();
        resolution.Selected.ShouldHaveSingleItem().DocumentId.ShouldBe("iade-v2");
        resolution.Discarded.ShouldHaveSingleItem().DocumentId.ShouldBe("iade-v1");
        resolution.Discarded[0].Reason.ShouldBe("2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.");
    }

    /// <summary>
    /// "Kargo ücreti ne kadar?" sorusu kargo politikasının yanında iade politikasının "İade Kargo Ücreti" bölümünü de (1.0
    /// ve 2.0) getirir; dolayısıyla iade ailesi için bir sürüm kararı verilir. Yanıt yalnızca kargo dokümanına dayandığında
    /// ise <c>versionResolution</c> boş kalmalıdır: <c>Applied=false</c>, seçilen ve elenen sürüm yok.
    /// </summary>
    /// <remarks>
    /// Sürüm kararları yanıtın kaynaklarını açıklamak için vardır. Yanıtın dayanmadığı bir aileyi raporlamak kullanıcıya
    /// "eski iade politikası elendi" gibi alakasız bir açıklama gösterir ve değerlendirme aracının "elenen sürüm raporlandı
    /// mı" kontrolünü yanıltır. Test, handler'ın kararları yalnızca atıf yapılan doküman ailelerine süzdüğünü korur.
    /// </remarks>
    [Fact]
    public async Task Version_resolution_lists_only_document_families_the_answer_is_based_on()
    {
        // "kargo ücreti" iade politikasının kargo bölümünü de (v1 ve v2) getirir; ancak yanıt kargo dokümanına atıf yapar.
        _generator.Respond = (_, context) =>
        {
            var shipping = context.First(source => source.Chunk.DocumentId == "kargo");
            return FakeAnswerGenerator.Answer("750 TL ve üzeri siparişlerde kargo ücretsizdir.", new GeneratedCitation(shipping.Label, shipping.Chunk.Content));
        };

        var result = await AskAsync("Kargo ücreti ne kadar?");

        result.Data!.Sources.ShouldHaveSingleItem().DocumentId.ShouldBe("kargo");
        result.Data.VersionResolution.Applied.ShouldBeFalse();
        result.Data.VersionResolution.Discarded.ShouldBeEmpty();
        result.Data.VersionResolution.Selected.ShouldBeEmpty();
    }

    /// <summary>
    /// Başarılı bir yanıtın dayandığı kaynağı eksiksiz tanımladığını doğrular: doküman kimliği (<c>iade-v2</c>), sürüm
    /// (2.0), yürürlük tarihi (2025-06-01), bölüm ("2. İade Süresi") ve alıntının kaynakta birebir geçtiğini gösteren
    /// <c>QuoteVerified=true</c>. Yanıt metninin güncel kuralı (30 gün) taşıdığı ve <c>refusalReason</c> alanının boş
    /// olduğu da kontrol edilir.
    /// </summary>
    /// <remarks>
    /// "Her yanıt kullanılan dokümanı ve ilgili bölümü göstermelidir" gereksinimini korur. Fake üretici varsayılan olarak
    /// bağlamdaki ilk kaynağı alıntıladığından test, sürüm çözümlemeden sonra ilk sırada güncel sürümün "İade Süresi"
    /// bölümünün kaldığını da dolaylı olarak doğrular.
    /// </remarks>
    [Fact]
    public async Task An_answer_names_the_cited_document_version_and_section()
    {
        var result = await AskAsync("İade süresi kaç gün?");

        result.Data!.Answerable.ShouldBeTrue();
        result.Data.RefusalReason.ShouldBeEmpty();
        result.Data.Answer.ShouldBe("Ürünü teslim aldıktan sonra 30 gün içinde iade edebilirsiniz.");
        var source = result.Data.Sources.ShouldHaveSingleItem();
        source.DocumentId.ShouldBe("iade-v2");
        source.Version.ShouldBe("2.0");
        source.EffectiveDate.ShouldBe(new DateOnly(2025, 6, 1));
        source.Section.ShouldBe("2. İade Süresi");
        source.QuoteVerified.ShouldBeTrue();
    }

    /// <summary>
    /// Kapı 1'i geçen bir soruda modelin <c>answerable=false</c> döndürdüğü durumu (Kapı 2) sınar. Model tam bir kez
    /// çağrılmalı; yanıt <c>ModelInsufficientContext</c> gerekçeli açık bir ret olmalı; modelin eksik bilgi açıklaması
    /// <c>missingInformation</c> alanına taşınmalı; yanıt metni sabit "yeterli bilgi bulunamadı" mesajı, kaynak listesi boş
    /// olmalıdır.
    /// </summary>
    /// <remarks>
    /// "Dokümanlarda yeterli bilgi yoksa yanıt üretmek yerine bunu açıkça söyle" gereksiniminin model tarafındaki
    /// karşılığıdır. Ret yanıtı sürüm kararı taşımaz (açıklanacak bir yanıt yoktur), ancak tanılama (diagnostics) modele
    /// hangi bağlamın gösterildiğini yine raporlar; böylece retlerin nedeni sonradan incelenebilir.
    /// </remarks>
    [Fact]
    public async Task When_the_model_finds_the_sources_insufficient_the_answer_is_an_explicit_refusal()
    {
        // Soru Kapı 1'i geçer (her sözcüğü bilgi tabanında var); reddeden, modelin kendisidir.
        _generator.Respond = (_, _) => FakeAnswerGenerator.NotAnswerable("Kaynaklar bu soruyu yanıtlamıyor.");

        var result = await AskAsync("İade süresi kaç gün?");

        _generator.Calls.ShouldBe(1);
        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.ModelInsufficientContext);
        result.Data.MissingInformation.ShouldBe("Kaynaklar bu soruyu yanıtlamıyor.");
        result.Data.Answer.ShouldBe(Messages.Knowledge.NotEnoughInformation);
        result.Data.Sources.ShouldBeEmpty();

        // Sürüm kararları bir yanıtın kaynaklarını açıklar; yanıt yoksa açıklanacak bir şey de yoktur.
        result.Data.VersionResolution.Applied.ShouldBeFalse();
        result.Data.VersionResolution.Discarded.ShouldBeEmpty();
        result.Data.Diagnostics.Context.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Modelin <c>answerable=true</c> deyip yalnızca kendisine verilmemiş bir kaynağa ("C9") atıf yaptığı durumu (Kapı 3)
    /// sınar: geçerli atıf kalmadığı için yanıt <c>NoValidCitations</c> gerekçesiyle reddedilmeli ve hiçbir kaynak
    /// gösterilmemelidir.
    /// </summary>
    /// <remarks>
    /// Model, verilen bağlamın dışına atıf yaparak kaynaksız bir iddiayı kaynaklıymış gibi sunamamalıdır. Bu test kırılırsa
    /// uydurma etiketlerle "desteklenen" yanıtlar müşteriye kaynaklı bir yanıt olarak ulaşabilir.
    /// </remarks>
    [Fact]
    public async Task An_answer_citing_no_provided_source_is_refused()
    {
        _generator.Respond = (_, _) => FakeAnswerGenerator.Answer("30 gün.", new GeneratedCitation("C9", "30 gün"));

        var result = await AskAsync("İade süresi kaç gün?");

        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.NoValidCitations);
        result.Data.Sources.ShouldBeEmpty();
    }

    /// <summary>
    /// Modelin raporladığı dokümanlar arası çelişkinin sunucu tarafında öncelik kuralıyla (<c>SourcePrecedence</c>)
    /// denetlendiğini doğrular. Senaryo gerçek bir çelişkidir: güncel iade politikası iade kargosunun ücretsiz olduğunu,
    /// eski tarihli SSS ise ücretin müşteriye ait olduğunu söyler. Model politikayı seçip SSS'yi reddederse
    /// <c>RuleSatisfied=true</c>, tersini yaparsa <c>RuleSatisfied=false</c> olmalıdır; seçilen ve reddedilen kaynaklar
    /// etiketlerinden doküman kimliklerine çözülerek raporlanır.
    /// </summary>
    /// <remarks>
    /// Modelin çelişki çözümüne körü körüne güvenilmez; seçimin kurala uyup uymadığı API yanıtında açıkça gösterilir. Bu
    /// test kırılırsa daha az yetkili bir SSS'yi politikaya tercih eden bir model yanıtı işaretlenmeden geçer.
    /// </remarks>
    [Theory]
    [InlineData("iade-v2", "5. İade Kargo Ücreti", "sss", "İade > İade kargo ücretini kim öder?", true)]
    [InlineData("sss", "İade > İade kargo ücretini kim öder?", "iade-v2", "5. İade Kargo Ücreti", false)]
    public async Task Conflicts_reported_by_the_model_are_checked_against_the_precedence_rule(
        string chosenDocument, string chosenSection, string rejectedDocument, string rejectedSection, bool ruleSatisfied)
    {
        _generator.Respond = (_, context) =>
        {
            var chosen = LabelOf(context, chosenDocument, chosenSection);
            var rejected = LabelOf(context, rejectedDocument, rejectedSection);
            return FakeAnswerGenerator.Answer("İade kargosu ücretsizdir.", new GeneratedCitation(chosen, "kargo")) with
            {
                Conflicts = [new GeneratedConflict("İade kargo ücreti", chosen, [rejected], "Politika daha yeni ve SSS'den önceliklidir.")]
            };
        };

        var result = await AskAsync("İade kargo ücretini kim öder?");

        var conflict = result.Data!.Conflicts.ShouldHaveSingleItem();
        conflict.Chosen.DocumentId.ShouldBe(chosenDocument);
        conflict.Rejected.ShouldHaveSingleItem().DocumentId.ShouldBe(rejectedDocument);
        conflict.RuleSatisfied.ShouldBe(ruleSatisfied);
    }

    /// <summary>
    /// Dil modeli hatalarının kendi durum kodlarıyla raporlandığını doğrular: sunucuya ulaşılamaması (<c>Unavailable</c>)
    /// 503 ve <c>LlmUnavailable</c> mesajını, yeniden denemeye rağmen geçersiz çıktı (<c>InvalidOutput</c>) 502 ve
    /// <c>LlmInvalidOutput</c> mesajını üretir; her iki durumda da <c>success=false</c> olur.
    /// </summary>
    /// <remarks>
    /// İki durumun ayrılması istemciye ve operatöre doğru sinyali verir: 503 "servis geçici olarak yok, sonra tekrar dene",
    /// 502 "model geçerli bir yanıt üretemedi" demektir. Bu test kırılırsa model hataları ayırt edilemeyen genel bir hataya
    /// dönüşür ya da işlenmemiş bir istisna olarak handler'ın dışına sızar.
    /// </remarks>
    [Theory]
    [InlineData(AnswerGenerationFailure.Unavailable, 503, Messages.Knowledge.LlmUnavailable)]
    [InlineData(AnswerGenerationFailure.InvalidOutput, 502, Messages.Knowledge.LlmInvalidOutput)]
    public async Task Model_failures_are_reported_with_their_own_status(AnswerGenerationFailure failure, int statusCode, string message)
    {
        _generator.Failure = new AnswerGenerationException(failure, "model error");

        var result = await AskAsync("İade süresi kaç gün?");

        result.Success.ShouldBeFalse();
        result.StatusCode.ShouldBe(statusCode);
        result.Message.ShouldBe(message);
    }

    /// <summary>
    /// Yanıtlanan bir sorunun <c>QuestionLog</c> tablosuna tam olarak bir kez yazıldığını doğrular: soru metni,
    /// <c>answerable</c> bayrağı ve yanıtın tamamının JSON hâli (burada kullanılan <c>iade-v2</c> kaynağını içerir). Kayıt,
    /// handler'ınkinden farklı yeni bir <c>DbContext</c> ile okunur; böylece verinin yalnızca change tracker'da kalmadığı,
    /// gerçekten veritabanına kaydedildiği kanıtlanır.
    /// </summary>
    /// <remarks>
    /// Soru logu denetim (audit) ve değerlendirme için tutulur: hangi soruya hangi kaynaklarla ne yanıt verildiği sonradan
    /// incelenebilmelidir. Bu test yanıtlanan yolu kapsar; ret yanıtları da handler'da aynı kayıt yolundan geçer.
    /// </remarks>
    [Fact]
    public async Task Every_answered_question_is_logged()
    {
        await AskAsync("İade süresi kaç gün?");

        await using var context = CreateContext();
        var log = await context.Set<Knowledge.Domain.Entities.QuestionLog>().SingleAsync(TestContext.Current.CancellationToken);
        log.Question.ShouldBe("İade süresi kaç gün?");
        log.Answerable.ShouldBeTrue();
        log.ResponseJson.ShouldContain("iade-v2");
    }

    /// <summary>
    /// Geçersiz soruların arama ve modele hiç ulaşmadan 400 ile reddedildiğini doğrular: yalnızca boşluktan oluşan soru
    /// "Soru boş olamaz.", 500 karakteri aşan soru "Soru en fazla 500 karakter olabilir." mesajını alır. Öznitelik
    /// argümanları derleme zamanı sabiti olmak zorunda olduğundan <c>new string('a', 501)</c> doğrudan
    /// <c>[InlineData]</c> içine yazılamaz; "çok uzun" bir yer tutucudur ve test gövdesi onu 501 karakterlik bir metne
    /// çevirir.
    /// </summary>
    /// <remarks>
    /// Uzunluk sınırı (<c>MaxQueryLength</c>) embedding ve dil modeli çağrılarını aşırı büyük girdilerden ve gereksiz
    /// maliyetten korur. Mesajlar birebir karşılaştırıldığı için merkezi Türkçe mesajlardaki istenmeyen bir değişiklik de
    /// burada fark edilir.
    /// </remarks>
    [Theory]
    [InlineData("   ", "Soru boş olamaz.")]
    [InlineData("çok uzun", "Soru en fazla 500 karakter olabilir.")]
    public async Task Invalid_questions_are_rejected(string question, string message)
    {
        var result = await AskAsync(question == "çok uzun" ? new string('a', 501) : question);

        result.StatusCode.ShouldBe(400);
        result.Message.ShouldBe(message);
    }

    /// <summary>
    /// Soruyla eşleşen tek dokümanın yürürlükten kalkmış (superseded) olduğu ve ailesinde güncel bir sürüm bulunmadığı
    /// durumu sınar. Soru Kapı 1'i geçer (sözcükleri birebir eşleşir), ancak sürüm çözümlemeden sonra modele verilebilecek
    /// bir kaynak kalmaz; yanıt <c>NoSourceInEffect</c> gerekçesiyle reddedilmeli ve model hiç çağrılmamalıdır.
    /// </summary>
    /// <remarks>
    /// Kural tek sürümlü aileler için de geçerlidir: geçersiz kılınmış bir doküman, ardılı bulunmasa bile asla kullanılmaz.
    /// Bu test kırılırsa eski bir kural (burada 20 TL'lik hediye paketi ücreti) modele ulaşıp güncelmiş gibi
    /// yanıtlanabilir. Senaryo bu teste özgü olduğundan ayrı bir indeks kurulur.
    /// </remarks>
    [Fact]
    public async Task Questions_matching_only_outdated_sources_are_refused_without_calling_the_model()
    {
        var index = new KnowledgeIndex(new FakeTextEmbedder(enabled: false), Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);
        index.Rebuild([Document("eski", "eski", "1.0", new DateOnly(2024, 1, 1), DocumentStatus.Superseded, DocumentCategory.Policy,
            ("Hediye Paketi", "Hediye paketi ücreti 20 TL'dir."))]);

        var result = await AskAsync("Hediye paketi ücreti ne kadar?", index);

        _generator.Calls.ShouldBe(0);
        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.NoSourceInEffect);
    }

    /// <summary>
    /// Modelin yanıt metni yalnızca bir kaynak işaretinden (<c>[C1]</c>) oluştuğunda temizlikten sonra metin boş kalır; bu
    /// durumda yanıt olarak atıfın alıntı metninin kullanıldığını doğrular. Buradaki alıntı kaynak bölümle birebir aynıdır
    /// (yani doğrulanmıştır) ve yanıt "Ürünü teslim aldıktan sonra 30 gün içinde iade edebilirsiniz." olur.
    /// </summary>
    /// <remarks>
    /// Bu geri dönüş (fallback) olmasaydı, kaynağı doğru bulup metne yalnızca işaret yazan bir model yanıtı ya boş metinle
    /// döner ya da gereksiz yere reddedilirdi. Alıntı kaynakta geçen metin olduğundan yanıt yine belgelere dayalı kalır.
    /// </remarks>
    [Fact]
    public async Task When_cleaning_leaves_no_answer_text_the_verified_quote_is_used()
    {
        _generator.Respond = (_, context) =>
            FakeAnswerGenerator.Answer("[C1]", new GeneratedCitation(context[0].Label, context[0].Chunk.Content));

        var result = await AskAsync("İade süresi kaç gün?");

        result.Data!.Answerable.ShouldBeTrue();
        result.Data.Answer.ShouldBe("Ürünü teslim aldıktan sonra 30 gün içinde iade edebilirsiniz.");
    }

    /// <summary>
    /// İndeks henüz kurulmamışken (ör. açılıştaki ingestion bitmeden ya da başarısız olduktan sonra) gelen bir sorunun
    /// beklemeden 503 ile reddedildiğini doğrular. Sorular indeks hazır olana kadar yanıtlanmaz; istemci daha sonra tekrar
    /// dener ya da operatör yeniden indeksleme başlatır.
    /// </summary>
    /// <remarks>
    /// Kurulmamış bir indeksle arama yapmak ya "yeterli bilgi yok" gibi yanıltıcı bir ret üretir ya da indeks istisna
    /// fırlattığı için 500'e dönüşürdü. 503, sorunun geçici ve sistem kaynaklı olduğunu açıkça belirtir.
    /// </remarks>
    [Fact]
    public async Task Questions_wait_for_the_index()
    {
        var emptyIndex = new KnowledgeIndex(new FakeTextEmbedder(enabled: false), Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);

        var result = await AskAsync("İade süresi kaç gün?", emptyIndex);

        result.StatusCode.ShouldBe(503);
    }
}
