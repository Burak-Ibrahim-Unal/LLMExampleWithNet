using System.Text.Json;
using Knowledge.Application.Contracts;
using Shared.Application.Common;
using Shouldly;
using SupportAssistant.Eval;

namespace SupportAssistant.UnitTests.Evaluation;

/// <summary>
/// Değerlendirme raporundaki halüsinasyon sinyali özetinin (<see cref="HallucinationSignals"/>) birim testleri.
/// </summary>
/// <remarks>
/// Sonuçlar elle yazılmış kontrol listeleriyle değil, gerçek <see cref="EvalChecks.Evaluate"/> çıktısıyla kurulur: bir
/// kontrolün adı değişirse özet o kontrolü sessizce kaçırmak yerine bu testler kırılır.
/// </remarks>
public sealed class HallucinationSignalsTests
{
    /// <summary>Sayı kontrolünün okuduğu tek doküman: güncel iade politikası (30 gün).</summary>
    private static readonly IReadOnlyDictionary<string, string> Documents = new Dictionary<string, string>
    {
        ["iade-v2"] = "İade Politikası 2.0 2025-06-01 2. İade Süresi Ürünü teslim aldıktan sonra 30 gün içinde iade edebilirsiniz."
    };

    /// <summary>
    /// Her durumu bir kez içeren koşu: cevapsız bir soruya verilmiş yanıt (U01), kaynakta olmayan bir sayı ve yasak bir
    /// ifade taşıyan yanıt (N01), alıntısı doğrulanamamış yanıt (N02), temiz bir yanıt (N03), yanıtlanabilir bir soruda ret
    /// (N04) ve hata zarfı (N05).
    /// </summary>
    private static readonly QuestionResult[] Results =
    [
        Result("U01", "cevapsiz", new EvalExpectation(false), Answer("Yasal cayma hakkı 14 gündür.", sources: ["iade-v2"])),
        Result("N01", "normal", new EvalExpectation(true, SourcesAnyOf: ["iade-v2"], MustNotContain: ["45 gün"]), Answer("İade süresi 45 gündür.", sources: ["iade-v2"])),
        Result("N02", "normal", new EvalExpectation(true, SourcesAnyOf: ["iade-v2"]), Answer("Ürünü 30 gün içinde iade edebilirsiniz.", sources: ["iade-v2"], quoteVerified: false)),
        Result("N03", "normal", new EvalExpectation(true, SourcesAnyOf: ["iade-v2"]), Answer("Ürünü 30 gün içinde iade edebilirsiniz.", sources: ["iade-v2"])),
        Result("N04", "normal", new EvalExpectation(true, SourcesAnyOf: ["iade-v2"]), Answer(Messages.Knowledge.NotEnoughInformation, answerable: false)),
        new(new EvalQuestion("N05", "normal", "soru", "beklenen", new EvalExpectation(true)), 502, "Dil modeli yanıt vermedi.", null,
            [new CheckResult("yanıt", false, "HTTP 502")], 1000, null, null)
    ];

    /// <summary>
    /// Paydanın yalnızca yanıt verilen sorular olduğunu (ret ve hata zarfı halüsinasyon olamaz), birden çok sinyal taşıyan
    /// bir yanıtın oranda bir kez sayıldığını ve her sinyalin sorusuyla ve türüyle, kontrollerin sırasıyla listelendiğini
    /// doğrular.
    /// </summary>
    /// <remarks>
    /// Yanıtlanabilir bir sorudaki ret (N04) bir kaçırmadır, uydurma değildir; oranın paydasına girseydi oran yanlışlıkla
    /// düşük görünürdü. Cevapsız bir soruya verilen yanıt (U01) ise bilgi tabanında dayanağı olmayan bir yanıttır; içerik
    /// kontrolleri o soruda hiç çalışmadığı için sinyal doğrudan beklentiden çıkarılır.
    /// </remarks>
    [Fact]
    public void Only_answered_questions_are_counted_and_each_signal_names_its_question()
    {
        var summary = HallucinationSignals.Summarize(Results);

        summary.Answered.ShouldBe(4);
        summary.Flagged.ShouldBe(3);
        summary.Signals.Select(signal => (signal.QuestionId, signal.Kind)).ShouldBe(
        [
            ("U01", "cevapsız soruya yanıt"),
            ("N01", "sayılar kaynakta"),
            ("N01", "yasak ifade"),
            ("N02", "alıntı doğrulandı")
        ]);
    }

    /// <summary>
    /// Raporun oranı özet satırında, her sinyali de kendi tablosunda gösterdiğini; <c>results.json</c>'ın aynı özeti
    /// taşıdığını ve sinyalsiz bir koşuda boş tablo yerine açık bir cümle yazıldığını doğrular.
    /// </summary>
    [Fact]
    public async Task The_report_shows_the_rate_and_lists_every_signal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "eval-report-" + Guid.NewGuid().ToString("N"));
        var options = new EvalOptions("http://localhost:5031", "eval/questions.json", "eval/results", null);

        try
        {
            await ReportWriter.WriteAsync(directory, options, null, Results);
            var report = await File.ReadAllTextAsync(Path.Combine(directory, "report.md"), TestContext.Current.CancellationToken);
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "results.json"), TestContext.Current.CancellationToken));

            report.ShouldContain("**Halüsinasyon sinyali:** 3/4 yanıtta");
            report.ShouldContain("| U01 | cevapsız soruya yanıt |");
            report.ShouldContain("| N01 | sayılar kaynakta | atıf yapılan dokümanlarda olmayan: 45 |");
            json.RootElement.GetProperty("hallucination").GetProperty("flagged").GetInt32().ShouldBe(3);

            await ReportWriter.WriteAsync(directory, options, null, [Results[3]]);
            var clean = await File.ReadAllTextAsync(Path.Combine(directory, "report.md"), TestContext.Current.CancellationToken);

            clean.ShouldContain("**Halüsinasyon sinyali:** 0/1 yanıtta");
            clean.ShouldContain("Hiçbir yanıtta sinyal yok.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Bir soruyu, verilen yanıtı ve bu yanıta gerçek kontrollerin uygulanmış sonucunu taşıyan bir koşu sonucu kurar.
    /// </summary>
    private static QuestionResult Result(string id, string category, EvalExpectation expect, AnswerDto answer) =>
        new(new EvalQuestion(id, category, "soru", "beklenen", expect), 200, "ok", answer, EvalChecks.Evaluate(expect, answer, "soru", Documents), 1000, null, null);

    /// <summary>
    /// Kontrollerin okuduğu alanlar (yanıt metni, <c>answerable</c>, atıf yapılan dokümanlar, alıntı doğrulama durumu) dışında
    /// sabit değerler taşıyan bir API yanıtı kurar.
    /// </summary>
    private static AnswerDto Answer(string text, bool answerable = true, string[]? sources = null, bool quoteVerified = true) => new(
        "soru",
        answerable,
        text,
        (sources ?? []).Select(id => new AnswerSourceDto(id, "Başlık", "1.0", new DateOnly(2025, 1, 1), "active", "politika", "Bölüm", "alıntı", quoteVerified)).ToList(),
        new VersionResolutionDto(false, "kural", [], []),
        [],
        string.Empty,
        answerable ? string.Empty : "LowRelevance",
        new AnswerDiagnosticsDto("hybrid", 0.7, 1.0, [], [], "model", 100, null, null, 1));
}
