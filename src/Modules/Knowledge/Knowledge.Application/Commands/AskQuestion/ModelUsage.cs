using Knowledge.Application.Abstractions;

namespace Knowledge.Application.Commands.AskQuestion;

/// <summary>
/// Bir sorudaki gerçek model isteklerinin sayısını ve toplam token kullanımını biriktirir; soru başına çağrı bütçesi
/// ve tanılamadaki <c>modelCalls</c> buradan gelir.
/// </summary>
/// <remarks>
/// Sayım üreticinin bildirdiği gerçek istek sayısıyla (<see cref="GeneratedAnswer.Attempts"/>) yapılır; şema
/// düzeltmesi için yapılan yeniden deneme de sayılır. Yalnızca son çağrının token sayısını raporlamak düzeltme turunun
/// maliyetini gizlerdi. Sağlayıcı kullanım bilgisi döndürmezse toplam null kalır; bilinmeyen bir değer 0 diye
/// gösterilmez.
/// </remarks>
internal sealed class ModelUsage
{
    /// <summary>Şimdiye kadar sunucuya giden model isteği sayısı (üreticinin yeniden denemeleri dahil).</summary>
    public int Calls { get; private set; }

    /// <summary>Toplam girdi token sayısı; hiçbir çağrı bildirmediyse null.</summary>
    public long? InputTokens { get; private set; }

    /// <summary>Toplam çıktı token sayısı; hiçbir çağrı bildirmediyse null.</summary>
    public long? OutputTokens { get; private set; }

    /// <summary>
    /// Bir model yanıtını sayaçlara ekler; yanıtın gerektirdiği istek sayısı kadar bütçe harcanır (en az bir).
    /// </summary>
    public void Add(GeneratedAnswer generated)
    {
        Calls += Math.Max(1, generated.Attempts);
        InputTokens = Sum(InputTokens, generated.InputTokens);
        OutputTokens = Sum(OutputTokens, generated.OutputTokens);
    }

    /// <summary>Bilinen değerleri toplar; yeni değer bilinmiyorsa (null) mevcut toplamı korur.</summary>
    private static long? Sum(long? total, long? value) => value is null ? total : (total ?? 0) + value;
}
