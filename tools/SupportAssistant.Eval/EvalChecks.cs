using Knowledge.Application.Contracts;
using Knowledge.Application.Text;

namespace SupportAssistant.Eval;

/// <summary>
/// Tek bir yanıtı beklentisine karşı deterministik olarak denetler; puanlamada hiçbir dil modeli kullanılmaz.
/// </summary>
/// <remarks>
/// LLM-as-judge yerine kural tabanlı kontroller seçildi: sonuç her koşuda aynıdır, bir sorunun neden kaldığı açıkça
/// görülür ve sistemi oluşturan küçük modelin kendi çıktısını yargılaması gibi zayıf bir ölçüte dayanılmaz.
/// </remarks>
public static class EvalChecks
{
    /// <summary>
    /// Bir yanıt için kontrol listesini üretir: yanıtlanabilirlik, beklenen kaynak(lar), yasak kaynaklar, zorunlu ifade
    /// grupları, yasak ifadeler ve elenmesi gereken eski sürümler. Her kontrol, raporda gösterilecek Türkçe bir ad ve
    /// beklenen/gerçek özetiyle döner.
    /// </summary>
    /// <remarks>
    /// Kaynak kontrolleri yanıt metnine değil <c>sources</c> alanındaki doküman kimliklerine bakar; "doğru cevap" ile
    /// "doğru kaynak" birlikte denetlenir. Elenen sürüm kontrolü, çelişkili sorularda eski sürümün yalnızca
    /// kullanılmamasını değil, elendiğinin <c>versionResolution.discarded</c> içinde açıkça raporlanmasını da şart koşar.
    /// </remarks>
    public static IReadOnlyList<CheckResult> Evaluate(EvalExpectation expect, AnswerDto answer)
    {
        var checks = new List<CheckResult>
        {
            new("yanıtlanabilirlik", answer.Answerable == expect.Answerable,
                $"beklenen: {(expect.Answerable ? "yanıt" : "bilgi yok")}, gerçek: {(answer.Answerable ? "yanıt" : $"bilgi yok ({answer.RefusalReason})")}")
        };

        // Bir ret yalnızca reddin kendisiyle değerlendirilir; reddedilmiş (ya da yanıtlanmaması gerekirken yanıtlanmış)
        // bir sorunun içerik kontrolleri hiçbir şey söylemez.
        if (!expect.Answerable || !answer.Answerable)
        {
            return checks;
        }

        // Aynı dokümanın birden çok bölümüne atıf yapılabilir; kaynak kontrolleri doküman düzeyinde olduğu için tekilleştirilir.
        var cited = answer.Sources.Select(source => source.DocumentId).Distinct(StringComparer.Ordinal).ToList();

        if (expect.SourcesAnyOf is { Count: > 0 } anyOf)
        {
            checks.Add(new CheckResult("kaynak", anyOf.Any(cited.Contains), $"beklenen: {string.Join(" | ", anyOf)}; atıf: {Join(cited)}"));
        }

        if (expect.SourcesAllOf is { Count: > 0 } allOf)
        {
            checks.Add(new CheckResult("kaynaklar", allOf.All(cited.Contains), $"beklenen: {string.Join(" + ", allOf)}; atıf: {Join(cited)}"));
        }

        if (expect.ForbiddenSources is { Count: > 0 } forbidden)
        {
            var used = forbidden.Where(cited.Contains).ToList();
            checks.Add(new CheckResult("yasak kaynak yok", used.Count == 0, used.Count == 0 ? "yok" : $"atıf yapılmış: {Join(used)}"));
        }

        foreach (var group in expect.MustContain ?? [])
        {
            checks.Add(new CheckResult("içerik", group.Any(phrase => ContainsPhrase(answer.Answer, phrase)), $"'{string.Join("' | '", group)}'"));
        }

        foreach (var phrase in expect.MustNotContain ?? [])
        {
            checks.Add(new CheckResult("yasak ifade", !ContainsPhrase(answer.Answer, phrase), $"'{phrase}'"));
        }

        if (expect.DiscardedVersions is { Count: > 0 } discarded)
        {
            var reported = answer.VersionResolution.Discarded.Select(version => version.DocumentId).ToList();
            checks.Add(new CheckResult("eski sürüm elendi", discarded.All(reported.Contains), $"beklenen: {Join(discarded)}; raporlanan: {Join(reported)}"));
        }

        return checks;
    }

    /// <summary>
    /// Büyük/küçük harf, noktalama ve Türkçe karakterden bağımsız, bir kelime başında başlaması gereken ifade eşleşmesi:
    /// "ücretsiz", "ücretsizdir" ile eşleşir (Türkçe ekler); "30 gün" ise "300 gün" ile eşleşmez.
    /// </summary>
    /// <remarks>
    /// Metin ve ifade, aramada kullanılan <see cref="TurkishTextNormalizer"/> ile aynı biçime indirilir (tr-TR küçük harf,
    /// ç/ğ/ı/ö/ş/ü katlama, harf ve rakam dışındaki her dizi tek boşluk); modelin "kaç gün" ya da "kac gun" yazması fark
    /// etmez. Başa eklenen boşluk eşleşmenin bir kelimenin başında başlamasını zorunlu kılar; sonu ise bilinçli olarak açık
    /// bırakılır ki eklemeli Türkçede "karşıla" → "karşılar" / "karşılamaktadır" gibi çekimler yakalansın. Bunun bedeli,
    /// yalnızca sayıdan oluşan bir ifadenin aynı rakamlarla başlayan daha uzun bir sayıyla da eşleşmesidir ("750" →
    /// "7500"). Normalleştirme sonrası boş kalan ifade hiçbir zaman eşleşmez.
    /// </remarks>
    public static bool ContainsPhrase(string text, string phrase)
    {
        var normalizedPhrase = TurkishTextNormalizer.Normalize(phrase);

        return normalizedPhrase.Length > 0
            && (" " + TurkishTextNormalizer.Normalize(text)).Contains(" " + normalizedPhrase, StringComparison.Ordinal);
    }

    /// <summary>
    /// Aramanın beklenen kaynağı (ya da kaynakları) bulup bulmadığını söyler; beklenen kaynağı olmayan sorular (cevapsız
    /// sorular) için <c>null</c> döner.
    /// </summary>
    /// <remarks>
    /// <c>SourcesAllOf</c> tanımlıysa tüm dokümanların sonuçlarda olması gerekir (iki dokümana yayılan sorular); yoksa
    /// <c>SourcesAnyOf</c>'tan biri yeterlidir. Ölçüm yanıttan bağımsızdır: bir soru kaldığında hatanın erişimde mi
    /// (kaynak hiç bulunamadı) yoksa kapılarda ya da üretimde mi olduğunu ayırt etmeyi sağlar.
    /// </remarks>
    public static bool? RetrievalHit(EvalExpectation expect, IReadOnlyCollection<string> retrievedDocumentIds)
    {
        if (expect.SourcesAllOf is { Count: > 0 } allOf)
        {
            return allOf.All(retrievedDocumentIds.Contains);
        }

        if (expect.SourcesAnyOf is { Count: > 0 } anyOf)
        {
            return anyOf.Any(retrievedDocumentIds.Contains);
        }

        return null;
    }

    /// <summary>
    /// Kontrol ayrıntısı için değerleri virgülle birleştirir. Boş liste "—" olarak yazılır; ayrıntı metninde "hiçbiri"
    /// (ör. hiç atıf yapılmamış olması) eksik ya da kaybolmuş bir değer gibi görünmez.
    /// </summary>
    private static string Join(IReadOnlyCollection<string> values) => values.Count == 0 ? "—" : string.Join(", ", values);
}
