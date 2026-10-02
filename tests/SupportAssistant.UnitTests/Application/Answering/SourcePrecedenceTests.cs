using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Domain.Entities;
using Shouldly;

namespace SupportAssistant.UnitTests.Application.Answering;

/// <summary>
/// <see cref="SourcePrecedence"/> için birim testleri. Farklı dokümanlar çeliştiğinde hangisinin geçerli sayılacağını
/// belirleyen kural şudur: politika ve prosedür kılavuzdan, kılavuz SSS'den önceliklidir; aynı öncelik düzeyinde
/// yürürlük tarihi daha yeni olan kazanır. Handler, modelin raporladığı çelişki seçimini bu kuralla sunucu tarafında
/// denetler (<c>RuleSatisfied</c>).
/// </summary>
public sealed class SourcePrecedenceTests
{
    /// <summary>
    /// Yalnızca türü ve yürürlük yılı (1 Ocak) değişen bir bölüm oluşturur; <c>Outranks</c> kararını etkileyen iki girdi
    /// tam olarak bunlardır, diğer alanlar sabittir.
    /// </summary>
    private static IndexedChunk Chunk(DocumentCategory category, int year) =>
        new(Guid.NewGuid(), $"{category}-{year}", "key", "Başlık", "1.0", new DateOnly(year, 1, 1),
            DocumentStatus.Active, category, "Bölüm", "metin");

    /// <summary>
    /// Öncelik kuralını tablo hâlinde doğrular: yetki düzeyi farklıysa yetkili olan kazanır (politika ile SSS, her iki
    /// yönde); daha yeni tarihli bir SSS bile daha eski bir politikayı geçemez (yetki, güncellikten önce gelir); kılavuz
    /// SSS'yi geçer; politika ile prosedür aynı düzeydedir ve aralarında daha yeni olan kazanır.
    /// </summary>
    /// <remarks>
    /// SSS politikanın bir özetidir ve onun gerisinde kalabilir; esas olan, sahibinin onayladığı politika metnidir. Bu test
    /// kırılırsa daha yeni tarihli ama daha az yetkili bir SSS'ye dayanan çelişki seçimi "kurala uygun" diye işaretlenir.
    /// </remarks>
    [Theory]
    [InlineData(DocumentCategory.Policy, 2025, DocumentCategory.Faq, 2024, true)]
    [InlineData(DocumentCategory.Faq, 2024, DocumentCategory.Policy, 2025, false)]
    [InlineData(DocumentCategory.Policy, 2023, DocumentCategory.Faq, 2025, true)]  // yetki, güncellikten önce gelir
    [InlineData(DocumentCategory.Guide, 2025, DocumentCategory.Faq, 2025, true)]
    [InlineData(DocumentCategory.Procedure, 2025, DocumentCategory.Policy, 2024, true)] // aynı düzey: daha yeni olan kazanır
    [InlineData(DocumentCategory.Procedure, 2024, DocumentCategory.Policy, 2025, false)]
    public void Policies_and_procedures_outrank_guides_which_outrank_faqs_and_newer_wins_within_a_rank(
        DocumentCategory candidateCategory, int candidateYear, DocumentCategory otherCategory, int otherYear, bool expected)
    {
        SourcePrecedence.Outranks(Chunk(candidateCategory, candidateYear), Chunk(otherCategory, otherYear)).ShouldBe(expected);
    }
}
