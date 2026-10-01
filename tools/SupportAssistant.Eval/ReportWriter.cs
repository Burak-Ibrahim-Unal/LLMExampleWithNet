using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Knowledge.Application.Contracts;

namespace SupportAssistant.Eval;

/// <summary>Writes the expected-versus-actual comparison as markdown (for people) and JSON (raw results).</summary>
public static class ReportWriter
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly (string Key, string Title)[] Categories = [("normal", "Normal"), ("cevapsiz", "Cevapsız"), ("celiskili", "Çelişkili")];

    public static async Task WriteAsync(string directory, EvalOptions options, SystemStatusDto? status, IReadOnlyList<QuestionResult> results)
    {
        Directory.CreateDirectory(directory);

        var raw = new
        {
            GeneratedAt = DateTimeOffset.Now,
            options.BaseUrl,
            options.Label,
            Status = status,
            Passed = results.Count(result => result.Passed),
            Total = results.Count,
            Results = results.Select(result => new
            {
                result.Question,
                result.StatusCode,
                result.Message,
                result.Passed,
                result.Checks,
                result.LatencyMs,
                result.LexicalHit,
                result.HybridHit,
                result.Answer
            })
        };

        await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(raw, JsonOptions));
        await File.WriteAllTextAsync(Path.Combine(directory, "report.md"), BuildMarkdown(options, status, results));
    }

    private static string BuildMarkdown(EvalOptions options, SystemStatusDto? status, IReadOnlyList<QuestionResult> results)
    {
        var report = new StringBuilder();
        var passed = results.Count(result => result.Passed);

        report.AppendLine("# Değerlendirme Raporu").AppendLine();
        report.AppendLine($"- **Tarih:** {DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");
        report.AppendLine($"- **Çalıştırma:** {options.Label ?? "varsayılan yapılandırma"}");
        report.AppendLine($"- **Dil modeli:** {Value(status?.Llm.Model)} · **Embedding:** {Value(status?.Embeddings.Model)} · **Arama modu:** {Value(status?.Index.RetrievalMode)}");
        report.AppendLine($"- **Sonuç:** {passed}/{results.Count} soru geçti").AppendLine();

        report.AppendLine("## Özet").AppendLine();
        report.AppendLine("| Kategori | Soru | Geçen |").AppendLine("|---|---:|---:|");

        foreach (var (key, title) in Categories)
        {
            var inCategory = results.Where(result => result.Question.Category == key).ToList();
            report.AppendLine($"| {title} | {inCategory.Count} | {inCategory.Count(result => result.Passed)} |");
        }

        report.AppendLine($"| **Toplam** | **{results.Count}** | **{passed}** |").AppendLine();

        var withExpectedSource = results.Where(result => result.HybridHit is not null).ToList();
        report.AppendLine($"**Arama isabeti** (beklenen kaynak, sunucunun varsayılan topK değeri kadar arama sonucu içinde — sürüm çözümünden önce; {withExpectedSource.Count} soru): " +
            $"yalnız BM25 {withExpectedSource.Count(result => result.LexicalHit == true)}/{withExpectedSource.Count} · " +
            $"hibrit {withExpectedSource.Count(result => result.HybridHit == true)}/{withExpectedSource.Count}  ");
        report.AppendLine($"**Yanıt süresi:** medyan {Seconds(Median(results.Select(result => result.LatencyMs)))}, ortalama {Seconds((long)results.Average(result => result.LatencyMs))}, " +
            $"en uzun {Seconds(results.Max(result => result.LatencyMs))} (Kapı 1'de reddedilen sorular modele gitmediği için ~0 sn)").AppendLine();

        report.AppendLine("| ID | Kategori | Soru | Beklenen | Sonuç | Süre |").AppendLine("|---|---|---|---|---|---:|");

        foreach (var result in results)
        {
            report.AppendLine($"| {result.Question.Id} | {CategoryTitle(result.Question.Category)} | {Cell(result.Question.Question)} | " +
                $"{(result.Question.Expect.Answerable ? "yanıt" : "bilgi yok")} | {(result.Passed ? "✅" : "❌")} | {Seconds(result.LatencyMs)} |");
        }

        report.AppendLine().AppendLine("## Soru bazında karşılaştırma").AppendLine();

        foreach (var result in results)
        {
            AppendQuestion(report, result);
        }

        return report.ToString();
    }

    private static void AppendQuestion(StringBuilder report, QuestionResult result)
    {
        var question = result.Question;
        var answer = result.Answer;

        report.AppendLine($"### {question.Id} · {CategoryTitle(question.Category)} · {(result.Passed ? "✅ geçti" : "❌ kaldı")}").AppendLine();
        report.AppendLine($"**Soru:** {question.Question}  ");
        report.AppendLine($"**Beklenen:** {question.ExpectedAnswer}  ");

        if (answer is null)
        {
            report.AppendLine($"**Gerçek:** HTTP {result.StatusCode} — {result.Message}  ");
        }
        else
        {
            report.AppendLine($"**Gerçek:** {answer.Answer}{(answer.Answerable ? string.Empty : $" *(refusalReason: {answer.RefusalReason})*")}  ");

            if (!string.IsNullOrWhiteSpace(answer.MissingInformation))
            {
                report.AppendLine($"**Eksik bilgi (model):** {answer.MissingInformation}  ");
            }

            foreach (var source in answer.Sources)
            {
                report.AppendLine($"**Kaynak:** `{source.DocumentId}` v{source.Version} ({source.EffectiveDate:yyyy-MM-dd}) › {source.Section} — \"{source.Quote}\"" +
                    $"{(source.QuoteVerified ? " *(alıntı doğrulandı)*" : " *(alıntı birebir bulunamadı)*")}  ");
            }

            foreach (var discarded in answer.VersionResolution.Discarded)
            {
                report.AppendLine($"**Sürüm kararı:** `{discarded.DocumentId}` v{discarded.Version} elendi — {discarded.Reason}  ");
            }

            foreach (var conflict in answer.Conflicts)
            {
                report.AppendLine($"**Kaynaklar arası çelişki:** {conflict.Topic} — seçilen `{conflict.Chosen.DocumentId}`, elenen " +
                    $"{string.Join(", ", conflict.Rejected.Select(rejected => $"`{rejected.DocumentId}`"))}; kurala uygun: {(conflict.RuleSatisfied ? "evet" : "hayır")}  ");
            }
        }

        report.AppendLine($"**Kontroller:** {string.Join(" · ", result.Checks.Select(check => $"{(check.Passed ? "✅" : "❌")} {check.Name}{(check.Passed ? string.Empty : $" ({check.Detail})")}"))}  ");
        report.AppendLine($"**Arama isabeti:** BM25 {Hit(result.LexicalHit)} · hibrit {Hit(result.HybridHit)} · **Süre:** {Seconds(result.LatencyMs)}").AppendLine();
    }

    private static string CategoryTitle(string key) => Categories.FirstOrDefault(category => category.Key == key).Title ?? key;

    private static string Hit(bool? hit) => hit switch { true => "✅", false => "❌", null => "—" };

    // A single slow request (model warm-up, a busy shared server) skews the mean; the median shows the typical case.
    private static long Median(IEnumerable<long> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    }

    private static string Seconds(long milliseconds) => (milliseconds / 1000.0).ToString("0.0", Turkish) + " sn";

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);
}
