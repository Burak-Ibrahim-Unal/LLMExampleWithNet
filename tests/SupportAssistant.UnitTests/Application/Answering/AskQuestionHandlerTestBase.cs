using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Commands.AskQuestion;
using Knowledge.Application.Contracts;
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
/// <para>
/// Bu soyut sınıf ortak kurulumu taşır (bellek içi SQLite, sahte üretici, fixture indeksi ve yardımcılar); testler konuya
/// göre üç sınıfa ayrılmıştır: hattın akışı ve kapılar (<see cref="AskQuestionCommandHandlerTests"/>), alıntı denetimi
/// ve model çağrı bütçesi (<see cref="AskQuestionCitationTests"/>), kaynak önceliğinin zorlanması
/// (<see cref="AskQuestionConflictTests"/>).
/// </para>
/// </remarks>
public abstract class AskQuestionHandlerTestBase : IAsyncLifetime
{
    /// <summary>
    /// Bellek içi SQLite bağlantısı. <c>:memory:</c> veritabanı yalnızca onu açan bağlantı açık kaldığı sürece yaşar; bu
    /// yüzden bağlantı test boyunca açık tutulur ve her <c>AppDbContext</c> aynı bağlantıyı paylaşır.
    /// </summary>
    private protected readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>
    /// Dil modelinin yerine geçen fake. Testler davranışını <c>Respond</c>/<c>Failure</c> ile belirler; modelin çağrılıp
    /// çağrılmadığını ve neyi gördüğünü <c>Calls</c>/<c>LastContext</c> ile denetler.
    /// </summary>
    private protected readonly FakeAnswerGenerator _generator = new();

    /// <summary>
    /// Fixture dokümanlarıyla kurulan gerçek arama indeksi. Embedder devre dışı (<c>enabled: false</c>) olduğundan
    /// yalnızca BM25 ile çalışır: sıralama deterministiktir ve hiçbir embedding sunucusuna gerek yoktur.
    /// </summary>
    private protected readonly KnowledgeIndex _index = new(new FakeTextEmbedder(enabled: false), Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);

    /// <summary>
    /// Verilen bölümlerle bir doküman sürümü (<c>KnowledgeDocument</c>) oluşturur. Başlık ve içerik özeti (hash) kimlikten
    /// türetilir; testler yalnızca sürüm çözümlemeyi ve kaynak önceliğini etkileyen alanları (aile anahtarı, sürüm,
    /// yürürlük tarihi, durum, tür) açıkça yazar.
    /// </summary>
    private protected static KnowledgeDocument Document(string id, string key, string version, DateOnly effectiveDate, DocumentStatus status, DocumentCategory category, params (string Path, string Content)[] sections)
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
    private protected AppDbContext CreateContext() => new(
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
    /// <param name="generator">
    /// İsteğe bağlı farklı bir üretici; gerçek üreticiyi sahte bir sohbet istemcisiyle handler'a bağlayan akış testleri
    /// içindir. Verilmezse sınıfın sahte üreticisi kullanılır.
    /// </param>
    private protected async Task<ApiResult<AnswerDto>> AskAsync(string question, IKnowledgeIndex? index = null, IGroundedAnswerGenerator? generator = null)
    {
        await using var context = CreateContext();
        var options = Options.Create(new RetrievalOptions());
        var usedIndex = index ?? _index;
        var handler = new AskQuestionCommandHandler(
            usedIndex,
            generator ?? _generator,
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
    private protected static string LabelOf(IReadOnlyList<ContextChunk> context, string documentId, string section) =>
        context.Single(source => source.Chunk.DocumentId == documentId && source.Chunk.SectionPath == section).Label;

    /// <summary>Güncel iade politikasının iade kargosunun ücretsiz olduğunu söyleyen bölümü.</summary>
    private protected const string ReturnShippingSection = "5. İade Kargo Ücreti";

    /// <summary>Eski tarihli SSS'nin iade kargosunu müşteriye yükleyen, güncel politikayla çelişen bölümü.</summary>
    private protected const string FaqSection = "İade > İade kargo ücretini kim öder?";

    /// <summary>
    /// Bağlamdaki bir bölüme verilen alıntıyla atıf yapan bir <c>GeneratedCitation</c> oluşturur. Etiket bağlamdan
    /// bulunur; alıntı serbesttir, böylece testler doğrulanan ve doğrulanamayan alıntıları aynı yardımcıyla kurar.
    /// </summary>
    private protected static GeneratedCitation Cite(IReadOnlyList<ContextChunk> context, string documentId, string section, string quote) =>
        new(LabelOf(context, documentId, section), quote);

    /// <summary>
    /// Bir bölümün metnini hem yanıt hem birebir alıntı olarak kullanan, doğrulanmış tek atıflı bir model yanıtı
    /// oluşturur; düzeltme turundaki "doğru" yanıtı temsil eder.
    /// </summary>
    private protected static GeneratedAnswer AnswerFrom(IReadOnlyList<ContextChunk> context, string documentId, string section)
    {
        var source = context.Single(chunk => chunk.Chunk.DocumentId == documentId && chunk.Chunk.SectionPath == section);
        return FakeAnswerGenerator.Answer(source.Chunk.Content, new GeneratedCitation(source.Label, source.Chunk.Content));
    }

    /// <summary>
    /// Seçtiği bölüme dayanan (doğrulanmış alıntıyla) ve bir çelişki bildiren model yanıtı oluşturur: seçilen bölüm
    /// <paramref name="chosenDocument"/>, elenen bölüm <paramref name="rejectedDocument"/>. Seçimin kurala uyup uymadığı
    /// testin seçtiği belgelere bağlıdır; sunucu bunu kendisi hesaplar.
    /// </summary>
    private protected static GeneratedAnswer AnswerWithConflict(
        IReadOnlyList<ContextChunk> context, string chosenDocument, string chosenSection, string rejectedDocument, string rejectedSection)
    {
        var answer = AnswerFrom(context, chosenDocument, chosenSection);
        var chosen = answer.Citations[0].ChunkLabel;
        return answer with
        {
            Conflicts = [new GeneratedConflict("İade kargo ücreti", chosen, [LabelOf(context, rejectedDocument, rejectedSection)], "Modelin gerekçesi.")]
        };
    }
}
