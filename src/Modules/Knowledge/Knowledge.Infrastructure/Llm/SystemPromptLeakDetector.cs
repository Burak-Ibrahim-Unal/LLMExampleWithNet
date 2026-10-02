using Knowledge.Application.Text;

namespace Knowledge.Infrastructure.Llm;

/// <summary>
/// Model çıktısının sistem prompt'undan bir cümleyi tekrarlayıp tekrarlamadığını denetler (sistem prompt'u sızıntısı,
/// OWASP LLM07). Üretici, modelin serbest metin alanlarını (yanıt, eksik bilgi açıklaması, çelişki konusu ve gerekçesi)
/// bununla tarar; handler işaretlenen yanıtı göstermeden <c>UnsafeOutput</c> gerekçesiyle reddeder.
/// </summary>
/// <remarks>
/// <para>
/// Karşılaştırma altı sözcüklük ortak dizilerle yapılır: her iki metin <see cref="TurkishTextNormalizer"/> ile
/// normalleştirilir ve çıktıdaki herhangi bir altı sözcüklük dizi sistem prompt'unda da geçiyorsa çıktı işaretlenir.
/// Büyük/küçük harf, Türkçe karakter ve noktalama farkı tekrarı gizleyemez; cümlenin bir kısmının kopyalanması da
/// yakalanır. Eşik ölçülerek seçildi: üç canlı değerlendirme koşusundaki 59 gerçek model metninin hiçbiri sistem
/// prompt'uyla dört sözcüklük bir dizi bile paylaşmıyordu, altı sözcük bu yüzden yanlış alarma karşı pay bırakır.
/// </para>
/// <para>
/// Diziler sistem prompt'unun satırlarından ayrı ayrı çıkarılır (satır sınırını aşan yapay diziler oluşmaz) ve öncelik
/// kuralı (<see cref="AnswerPrompt.PrecedenceRule"/>) dışarıda bırakılır: o kural yanıtın açıklamasıdır, API onu zaten
/// yayımlar. Bu bir ilk savunma hattıdır; modelin talimatları başka sözcüklerle anlatması ya da çevirmesi yakalanmaz.
/// Asıl güvence yine yapısaldır: yanıt yalnızca doğrulanmış alıntılara dayanabilir.
/// </para>
/// </remarks>
internal static class SystemPromptLeakDetector
{
    /// <summary>Tekrar sayılan en kısa ortak sözcük dizisinin uzunluğu.</summary>
    private const int ShingleLength = 6;

    /// <summary>
    /// Sistem prompt'unun, öncelik kuralı dışındaki satırlarından çıkarılan altı sözcüklük normalleştirilmiş diziler.
    /// Prompt sabit olduğu için bir kez hesaplanır.
    /// </summary>
    private static readonly HashSet<string> PromptShingles = AnswerPrompt.System
        .Split('\n')
        .Where(line => !line.Contains(AnswerPrompt.PrecedenceRule, StringComparison.Ordinal))
        .SelectMany(Shingles)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Metin, sistem prompt'unun (öncelik kuralı hariç) herhangi bir yerindeki altı sözcüklük bir diziyi tekrarlıyorsa
    /// true döndürür.
    /// </summary>
    /// <param name="text">Modelin ürettiği serbest metin; boş olabilir.</param>
    public static bool Repeats(string text) => Shingles(text).Any(PromptShingles.Contains);

    /// <summary>
    /// Metni normalleştirip sözcüklerine ayırır ve ardışık her <see cref="ShingleLength"/> sözcüğü tek bir dizi olarak
    /// döndürür; daha kısa metinlerde hiç dizi üretmez.
    /// </summary>
    private static IEnumerable<string> Shingles(string text)
    {
        var words = TurkishTextNormalizer.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var start = 0; start + ShingleLength <= words.Length; start++)
        {
            yield return string.Join(' ', words, start, ShingleLength);
        }
    }
}
