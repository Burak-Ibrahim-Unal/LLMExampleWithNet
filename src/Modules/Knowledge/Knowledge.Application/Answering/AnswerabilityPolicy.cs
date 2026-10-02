using Knowledge.Application.Abstractions;
using Knowledge.Application.Options;
using Microsoft.Extensions.Options;

namespace Knowledge.Application.Answering;

/// <summary>
/// Kapı 1, dil modelinden önce: bilgi tabanında soruya yeterince yakın bir şey var mı? İki sinyalden biri yeterlidir.
/// Vektör benzerliği parafrazları yakalar ("paramı ne zaman alırım" → "para iadesi"); kelime kapsamı ise embedding
/// modelinin zayıf kaldığı durumları, örneğin Türkçe karakter kullanılmadan yazılmış soruları yakalar.
/// </summary>
/// <remarks>
/// Eşiğin altında kalan soru dil modeli hiç çağrılmadan reddedilir (<c>LowRelevance</c>): bu hem gereksiz LLM maliyetini
/// ve gecikmesini önler hem de alan dışı sorularda modelin "yardımsever" bir uydurma yapma riskini ortadan kaldırır.
/// Ham BM25 skoru sorgudan sorguya karşılaştırılamadığı için kelime sinyali olarak 0..1 aralığındaki idf ağırlıklı
/// kapsam kullanılır. Eşikler (<c>RetrievalOptions</c>) değerlendirme setiyle kalibre edildi. Alana yakın ama
/// dokümanlarda yanıtı olmayan sorular benzerlikle ayrılamadığı için bu kapıdan geçebilir; onları Kapı 2'de model reddeder.
/// Sınıf durumsuzdur ve tekil (singleton) olarak kaydedilir.
/// </remarks>
public sealed class AnswerabilityPolicy(IOptions<RetrievalOptions> options)
{
    /// <summary>
    /// Arama sonucunda yeterli kanıt varsa true döndürür: hibrit modda en iyi kosinüs benzerliği ≥ <c>MinDenseScore</c>
    /// (varsayılan 0.55) ya da en iyi kelime kapsamı ≥ <c>MinLexicalCoverage</c> (varsayılan 0.5).
    /// </summary>
    /// <remarks>
    /// Vektör sinyali yalnızca hibrit modda sayılır; lexical modda karar tamamen kelime kapsamına kalır. İki sinyalin
    /// VEYA ile birleştirilmesi bilinçlidir: her biri diğerinin kör noktasını kapatır, VE ile birleştirmek ise parafraz ve
    /// Türkçe karaktersiz soruları gereksiz yere reddederdi. Karar kodda, saf bir fonksiyon olarak verildiği için sınır
    /// değerleri birim testleriyle doğrulanabilir.
    /// </remarks>
    public bool HasEnoughEvidence(SearchResult result)
    {
        var settings = options.Value;
        var vectorEvidence = result.Mode == RetrievalMode.Hybrid && result.MaxDenseScore >= settings.MinDenseScore;
        var wordEvidence = result.MaxLexicalCoverage >= settings.MinLexicalCoverage;

        return vectorEvidence || wordEvidence;
    }
}
