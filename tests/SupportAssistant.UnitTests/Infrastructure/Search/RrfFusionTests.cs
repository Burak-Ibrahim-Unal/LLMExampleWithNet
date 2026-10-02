using Knowledge.Infrastructure.Search;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Search;

/// <summary>
/// <see cref="RrfFusion"/> birim testleri. Reciprocal Rank Fusion, BM25 ve vektör sıralamalarını skorlarına göre değil
/// sıralarına göre birleştirir; böylece birbirinden bağımsız ölçeklerde yaşayan BM25 skorlarını ve kosinüs
/// benzerliklerini birbirine göre kalibre etmek gerekmez. Test, formülü elle hesaplanabilen küçük tamsayı listeleriyle
/// doğrular.
/// </summary>
public sealed class RrfFusionTests
{
    /// <summary>
    /// Birden çok listede iyi sıralanan öğelerin öne geçtiğini ve skorların <c>Σ 1 / (k + sıra)</c> formülüne (1 tabanlı
    /// sıra, k = 60) birebir uyduğunu doğrular: iki listede de yer alan 1 ve 3, yalnızca sözcüksel listede bulunan 2'nin
    /// önüne geçer; 1 ise iki listedeki sıralarının toplam katkısı daha yüksek olduğu için 3'ün de önündedir.
    /// </summary>
    /// <remarks>
    /// Kesin skor kontrolleri sıra tabanındaki bir kaymayı (0 tabanlı sırada ilk öğe 1/61 yerine 1/60 alırdı) ve k
    /// sabitinin yanlış uygulanmasını yakalar. Hem sözcüksel hem anlamsal aramanın onayladığı bölümlerin öne çıkması
    /// hibrit aramanın amacıdır.
    /// </remarks>
    [Fact]
    public void Fuse_rewards_items_ranked_well_in_several_lists()
    {
        // sözcüksel (BM25): 1, 2, 3 — vektör: 3, 1
        var fused = RrfFusion.Fuse([[1, 2, 3], [3, 1]], k: 60);

        fused.Select(item => item.Item).ShouldBe([1, 3, 2]);
        fused[0].Score.ShouldBe(1.0 / 61 + 1.0 / 62, 1e-12);
        fused[1].Score.ShouldBe(1.0 / 63 + 1.0 / 61, 1e-12);
        fused[2].Score.ShouldBe(1.0 / 62, 1e-12);
    }
}
