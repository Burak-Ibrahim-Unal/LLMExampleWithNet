using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Knowledge.Application.Contracts;

namespace SupportAssistant.Eval;

/// <summary>
/// Beklenen ile gerçek karşılaştırmasını iki biçimde yazar: insanlar için Türkçe Markdown raporu (<c>report.md</c>) ve
/// ham sonuçlar için JSON (<c>results.json</c>).
/// </summary>
/// <remarks>
/// Rapor, değerlendiricinin her soruda beklenen ve gerçek yanıtı, atıf yapılan doküman/sürüm/bölümü, alıntının doğrulanıp
/// doğrulanmadığını, elenen sürümleri, kaynaklar arası çelişkileri ve kalan kontrolleri tek sayfada görmesi için
/// tasarlanmıştır. JSON aynı verinin eksiksiz kopyasıdır; rapordaki her satır ondan doğrulanabilir ve koşular araçlarla
/// karşılaştırılabilir.
/// </remarks>
public static class ReportWriter
{
    /// <summary>Süreleri Türkçe ondalık ayırıcıyla ("1,5 sn") biçimlendirmek için kullanılan kültür.</summary>
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// <c>results.json</c> için camelCase ve girintili JSON ayarları. <c>UnsafeRelaxedJsonEscaping</c>, Türkçe karakterlerin
    /// <c>\uXXXX</c> kaçış dizileri yerine olduğu gibi yazılmasını sağlar ve dosya okunabilir kalır. Adındaki "unsafe" HTML
    /// içine gömme riskini anlatır; diske yazılan bir rapor dosyası için geçerli değildir.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Soru dosyasındaki kategori anahtarlarının rapordaki Türkçe başlıkları. Dizi olması, özet tablosunun sırasını
    /// sabitler (normal → cevapsız → çelişkili).
    /// </summary>
    private static readonly (string Key, string Title)[] Categories = [("normal", "Normal"), ("cevapsiz", "Cevapsız"), ("celiskili", "Çelişkili")];

    /// <summary>
    /// Çıktı klasörünü (yoksa) oluşturur ve aynı sonuçları <c>results.json</c> ile <c>report.md</c> olarak yazar.
    /// JSON'a koşu bilgileri (zaman, API adresi, etiket, sağlık durumu), halüsinasyon sinyali özeti ve her soru için yanıtın
    /// tamamı dahil edilir.
    /// </summary>
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
            Hallucination = HallucinationSignals.Summarize(results),
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

    /// <summary>
    /// Markdown raporunu üretir: başlık (tarih, koşu etiketi, model/embedding/arama modu, toplam sonuç), kategori özeti,
    /// arama isabeti, yanıt süresi, düzeltme turu ve halüsinasyon sinyali satırları, soru tablosu, halüsinasyon sinyalleri
    /// tablosu ve soru bazında ayrıntılı karşılaştırma.
    /// </summary>
    /// <remarks>
    /// Arama isabeti yalnızca beklenen kaynağı olan sorular üzerinden ve iki mod için ayrı verilir; BM25 ile hibrit arasındaki
    /// fark vektör aramasının katkısını gösterir. Yanıt süresinde ortalamanın yanında medyan ve en uzun süre de yazılır;
    /// Kapı 1'de reddedilen soruların modele gitmediği için ~0 sn sürdüğü raporda açıkça not edilir. Düzeltme turuna giren
    /// (modelin iki kez çağrıldığı) soru sayısı da verilir. Halüsinasyon sinyalleri (bkz. <see cref="HallucinationSignals"/>)
    /// geçen/kalan sayısından ayrı raporlanır: kalan bir soru eksik ya da yanlış bir yanıttan, bir sinyal ise dayanaksız bir
    /// iddiadan haber verir.
    /// </remarks>
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

        // HybridHit null ise sorunun beklenen kaynağı yoktur (cevapsız sorular); bu sorular isabet oranının paydasına girmez.
        var withExpectedSource = results.Where(result => result.HybridHit is not null).ToList();
        report.AppendLine($"**Arama isabeti** (beklenen kaynak, sunucunun varsayılan topK değeri kadar arama sonucu içinde — sürüm çözümünden önce; {withExpectedSource.Count} soru): " +
            $"yalnız BM25 {withExpectedSource.Count(result => result.LexicalHit == true)}/{withExpectedSource.Count} · " +
            $"hibrit {withExpectedSource.Count(result => result.HybridHit == true)}/{withExpectedSource.Count}  ");
        // Boş bir soru dosyasında ortalama ve en uzun süre tanımsızdır (Average/Max boş kümede istisna fırlatır).
        if (results.Count > 0)
        {
            report.AppendLine($"**Yanıt süresi:** medyan {Seconds(Median(results.Select(result => result.LatencyMs)))}, ortalama {Seconds((long)results.Average(result => result.LatencyMs))}, " +
                $"en uzun {Seconds(results.Max(result => result.LatencyMs))} (Kapı 1'de reddedilen sorular modele gitmediği için ~0 sn)  ");
        }

        // Düzeltme turu (doğrulanamayan alıntı ya da öncelik ihlali yüzünden ikinci model çağrısı) gecikmeyi artırır ve
        // modelin ilk denemede kabul edilebilir bir yanıt veremediğini gösterir; sayısı ayrıca izlenir.
        var corrected = results.Count(result => result.Answer?.Diagnostics.ModelCalls > 1);
        report.AppendLine($"**Düzeltme turu:** {corrected} soruda model ikinci kez çağrıldı  ");

        // Desteksiz iddia oranı: en az bir sinyal taşıyan yanıt / yanıt verilen soru. Ret ve hata zarfları paydaya girmez.
        var hallucination = HallucinationSignals.Summarize(results);
        report.AppendLine($"**Halüsinasyon sinyali:** {hallucination.Flagged}/{hallucination.Answered} yanıtta (desteksiz iddia oranı; ayrıntı aşağıda)").AppendLine();

        report.AppendLine("| ID | Kategori | Soru | Beklenen | Sonuç | Süre |").AppendLine("|---|---|---|---|---|---:|");

        foreach (var result in results)
        {
            report.AppendLine($"| {result.Question.Id} | {CategoryTitle(result.Question.Category)} | {Cell(result.Question.Question)} | " +
                $"{(result.Question.Expect.Answerable ? "yanıt" : "bilgi yok")} | {(result.Passed ? "✅" : "❌")} | {Seconds(result.LatencyMs)} |");
        }

        report.AppendLine().AppendLine("## Halüsinasyon sinyalleri").AppendLine();
        report.AppendLine("Deterministik vekil ölçülerdir, kanıt değildir: cevapsız soruya yanıt, atıf yapılan dokümanlarda geçmeyen sayı, " +
            "kaynakta birebir bulunamayan alıntı ve yasak ifade (ters karar, eski kural ya da bilgi tabanında olmayan genel bilgi). " +
            "Sayı içermeyen ve yasak listesinde olmayan bir uydurma yakalanmaz; yanıtlar ayrıca elle okunur.").AppendLine();

        if (hallucination.Signals.Count == 0)
        {
            report.AppendLine("Hiçbir yanıtta sinyal yok.");
        }
        else
        {
            report.AppendLine("| ID | Sinyal | Ayrıntı |").AppendLine("|---|---|---|");

            foreach (var signal in hallucination.Signals)
            {
                report.AppendLine($"| {signal.QuestionId} | {signal.Kind} | {Cell(signal.Detail)} |");
            }
        }

        report.AppendLine().AppendLine("## Soru bazında karşılaştırma").AppendLine();

        foreach (var result in results)
        {
            AppendQuestion(report, result);
        }

        return report.ToString();
    }

    /// <summary>
    /// Tek bir soru için ayrıntılı karşılaştırma bloğu yazar: soru, beklenen ve gerçek yanıt (reddedildiyse
    /// <c>refusalReason</c>), modelin bildirdiği eksik bilgi, her kaynak için doküman/sürüm/tarih/bölüm ve alıntının
    /// doğrulanıp doğrulanmadığı, elenen sürümler ve nedenleri, kaynaklar arası çelişkiler ve öncelik kuralına uyum,
    /// kontroller, arama isabeti, süre ve model çağrısı sayısı.
    /// </summary>
    /// <remarks>
    /// Satır sonlarındaki iki boşluk Markdown'da satır kırılmasıdır; blok tek paragraf olarak okunur. Yalnızca kalan
    /// kontrollerin ayrıntısı yazılır; rapor kısa kalır ama bir sorunun neden kaldığı hemen görülür. Yanıt verisi yoksa
    /// (hata zarfı) gerçek yanıt yerine HTTP durum kodu ve API mesajı gösterilir.
    /// </remarks>
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
        report.AppendLine($"**Arama isabeti:** BM25 {Hit(result.LexicalHit)} · hibrit {Hit(result.HybridHit)} · **Süre:** {Seconds(result.LatencyMs)}" +
            $"{(answer is null ? string.Empty : $" · **Model çağrısı:** {answer.Diagnostics.ModelCalls}")}").AppendLine();
    }

    /// <summary>Kategori anahtarını Türkçe başlığa çevirir; tanımsız bir anahtar raporda olduğu gibi gösterilir.</summary>
    private static string CategoryTitle(string key) => Categories.FirstOrDefault(category => category.Key == key).Title ?? key;

    /// <summary>Arama isabetini simgeye çevirir; "—" sorunun beklenen kaynağı olmadığı için isabetin ölçülmediğini belirtir.</summary>
    private static string Hit(bool? hit) => hit switch { true => "✅", false => "❌", null => "—" };

    /// <summary>
    /// Sürelerin medyanını hesaplar (çift sayıda değerde ortadaki iki değerin ortalaması, boş listede 0).
    /// </summary>
    /// <remarks>
    /// Tek bir yavaş istek (modelin ısınması, meşgul ve paylaşılan bir sunucu) ortalamayı çarpıtır; medyan tipik durumu
    /// gösterir. Raporda ortalama ve en uzun süre de verildiği için aykırı değerler gizlenmez.
    /// </remarks>
    private static long Median(IEnumerable<long> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    }

    /// <summary>Milisaniyeyi Türkçe biçimli saniyeye çevirir (ör. 1470 → "1,5 sn").</summary>
    private static string Seconds(long milliseconds) => (milliseconds / 1000.0).ToString("0.0", Turkish) + " sn";

    /// <summary>Boş ya da eksik değerleri (ör. sağlık ucundan okunamayan model adı) raporda "—" olarak gösterir.</summary>
    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    /// <summary>
    /// Metindeki <c>|</c> karakterini kaçışlar; aksi hâlde soru metnindeki bir dikey çizgi Markdown tablosunun sütunlarını
    /// bozardı.
    /// </summary>
    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);
}
