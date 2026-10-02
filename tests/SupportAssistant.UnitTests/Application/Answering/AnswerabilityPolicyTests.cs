using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.Options;
using Microsoft.Extensions.Options;
using Shouldly;

namespace SupportAssistant.UnitTests.Application.Answering;

/// <summary>
/// <see cref="AnswerabilityPolicy"/> (Kapı 1) için birim testleri. Politika, dil modeli çağrılmadan önce bilgi tabanında
/// soruya yeterince yakın bir bölüm olup olmadığına karar verir: en iyi yoğun (dense) kosinüs benzerliği eşiğini geçerse
/// YA DA en iyi sözcük kapsamı (lexical coverage) eşiğini geçerse kanıt yeterli sayılır.
/// </summary>
/// <remarks>
/// Testler gerçek bir arama yapmaz; politikanın okuduğu üç bilgiyi (arama modu, <c>MaxDenseScore</c>,
/// <c>MaxLexicalCoverage</c>) doğrudan bir <c>SearchResult</c> içinde verir. Böylece eşik mantığı embedding modelinden
/// ve BM25 hesabından bağımsız, deterministik biçimde sınanır.
/// </remarks>
public sealed class AnswerabilityPolicyTests
{
    /// <summary>
    /// Eşikler <c>RetrievalOptions</c> varsayılanlarıyla aynı değerlere (kosinüs ≥ 0.55, kapsam ≥ 0.5) açıkça sabitlenir.
    /// Böylece varsayılanlar ileride yeniden kalibre edilse bile bu test kalibrasyonu değil, "iki sinyalden biri yeter"
    /// mantığını ve eşiklerin kapsayıcı (≥) olduğunu sınamaya devam eder.
    /// </summary>
    private static readonly AnswerabilityPolicy Policy = new(Options.Create(new RetrievalOptions { MinDenseScore = 0.55, MinLexicalCoverage = 0.5 }));

    /// <summary>
    /// Kanıtın, vektör sinyali VEYA sözcük sinyali kendi eşiğini geçtiğinde yeterli sayıldığını doğrular. Satırlar sırasıyla
    /// şunları kapsar: yalnızca vektörlerin yakaladığı bir parafraz ("paramı ne zaman alırım" → "para iadesi"); Türkçe
    /// karakter kullanılmadan yazıldığı için embedding modelinin zayıf kaldığı ama sözcüklerin tam eşleştiği bir soru; iki
    /// sinyalin de zayıf olduğu alakasız bir soru; lexical (yalnızca BM25) modda kapsam eşiğinin hemen altı (0.49) ve tam
    /// sınırı (0.50, kapsayıcı).
    /// </summary>
    /// <remarks>
    /// Bu test iki yöndeki gerilemeyi yakalar: politika "VE" mantığına kayarsa parafrazlar ve Türkçe karaktersiz sorular
    /// gereksiz yere reddedilir; eşikler gevşerse alan dışı sorular modele ulaşır, hem LLM maliyeti doğar hem de modelin
    /// uydurma bir yanıt üretme riski ortaya çıkar. Politika vektör kanıtını yalnızca Hybrid modda saydığından lexical
    /// modda karar tek başına kapsama dayanır.
    /// </remarks>
    [Theory]
    [InlineData(RetrievalMode.Hybrid, 0.70, 0.10, true)]  // parafraz: yalnızca vektörler bulur
    [InlineData(RetrievalMode.Hybrid, 0.38, 1.00, true)]  // Türkçe karakter kullanılmadan yazılmış: yalnızca sözcükler bulur
    [InlineData(RetrievalMode.Hybrid, 0.43, 0.10, false)] // alakasız soru
    [InlineData(RetrievalMode.Lexical, 0.00, 0.49, false)]
    [InlineData(RetrievalMode.Lexical, 0.00, 0.50, true)]
    public void Evidence_is_enough_when_either_the_vector_or_the_word_signal_clears_its_threshold(
        RetrievalMode mode, double maxDenseScore, double maxLexicalCoverage, bool expected)
    {
        var result = new SearchResult(mode, [], maxDenseScore, maxLexicalCoverage);

        Policy.HasEnoughEvidence(result).ShouldBe(expected);
    }
}
