namespace SupportAssistant.Eval;

/// <summary>
/// Bir koşunun sonuçlarından halüsinasyon sinyallerini toplar: dayanağı olmayan ya da kaynağa göre yanlış olduğu bilinen
/// iddia taşıyan yanıtlar. Rapor bu özetten "desteksiz iddia oranını" (sinyal taşıyan yanıt / yanıt verilen soru) yazar.
/// </summary>
/// <remarks>
/// <para>
/// Dört sinyal deterministik vekil ölçülerdir, kanıt değildir:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Cevapsız soruya yanıt:</b> bilgi tabanında bilgisi olmayan bir soru yanıtlanmış; yanıtın dayanağı yoktur.
/// </description></item>
/// <item><description>
/// <b>Kaynakta olmayan sayı</b> ("sayılar kaynakta" kontrolü): yanıttaki bir sayı ne atıf yapılan dokümanlarda ne de soruda
/// geçiyor.
/// </description></item>
/// <item><description>
/// <b>Doğrulanamayan alıntı</b> ("alıntı doğrulandı" kontrolü): bir alıntı atıf yapılan bölümde birebir bulunamadı ya da
/// yanıt hiç kaynak göstermedi.
/// </description></item>
/// <item><description>
/// <b>Yasak ifade</b>: kaynağa göre yanlış olduğu bilinen bir ifade (ters karar, eski kural ya da bilgi tabanında olmayan
/// genel bilgi).
/// </description></item>
/// </list>
/// <para>
/// Payda yalnızca yanıt verilen sorulardır: ret ve hata zarfı sabit metin taşır, halüsinasyon olamaz. Yanıtlanabilir bir
/// sorudaki ret bir kaçırmadır; o, yanıtlanabilirlik kontrolünde ve geçen soru sayısında görünür. Sayı içermeyen ve yasak
/// listesinde olmayan bir uydurma bu sinyallere yakalanmaz; değerlendirme raporundaki yanıtlar bu yüzden ayrıca elle okunur.
/// </para>
/// </remarks>
public static class HallucinationSignals
{
    /// <summary>Cevapsız (bilgi tabanında bilgisi olmayan) bir soruya yanıt verilmesinin sinyal türü.</summary>
    public const string AnsweredUnanswerable = "cevapsız soruya yanıt";

    /// <summary>
    /// Kalması halüsinasyon sinyali sayılan kontrollerin adları (<see cref="EvalChecks"/>). Diğer kontroller (kaynak, bölüm,
    /// içerik, koşul) yanlış ya da eksik yanıtı gösterir ama yanıtta dayanaksız bir iddia olduğunu göstermez.
    /// </summary>
    private static readonly HashSet<string> SignalChecks = new(StringComparer.Ordinal) { "alıntı doğrulandı", "sayılar kaynakta", "yasak ifade" };

    /// <summary>
    /// Yanıt verilen soruları sayar ve her birindeki sinyalleri soru ve kontrol sırasıyla toplar.
    /// </summary>
    /// <remarks>
    /// Cevapsız bir soruya verilen yanıtta içerik kontrolleri çalışmaz (bkz. <see cref="EvalChecks.Evaluate"/>); sinyal bu
    /// yüzden kontrollerden değil sorunun beklentisinden çıkarılır ve ayrıntısı, yanıtın atıf yaptığı dokümanlardır.
    /// </remarks>
    public static HallucinationSummary Summarize(IReadOnlyList<QuestionResult> results)
    {
        var answered = results.Where(result => result.Answer is { Answerable: true }).ToList();
        var signals = new List<HallucinationSignal>();

        foreach (var result in answered)
        {
            if (!result.Question.Expect.Answerable)
            {
                var cited = result.Answer!.Sources.Select(source => source.DocumentId).Distinct(StringComparer.Ordinal).ToList();
                signals.Add(new HallucinationSignal(result.Question.Id, AnsweredUnanswerable, $"beklenen: bilgi yok; atıf: {(cited.Count == 0 ? "—" : string.Join(", ", cited))}"));
            }

            signals.AddRange(result.Checks
                .Where(check => !check.Passed && SignalChecks.Contains(check.Name))
                .Select(check => new HallucinationSignal(result.Question.Id, check.Name, check.Detail)));
        }

        return new HallucinationSummary(answered.Count, signals.Select(signal => signal.QuestionId).Distinct(StringComparer.Ordinal).Count(), signals);
    }
}
