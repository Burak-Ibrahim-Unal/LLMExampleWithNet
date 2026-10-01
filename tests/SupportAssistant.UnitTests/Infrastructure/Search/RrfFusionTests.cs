using Knowledge.Infrastructure.Search;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Search;

public sealed class RrfFusionTests
{
    [Fact]
    public void Fuse_rewards_items_ranked_well_in_several_lists()
    {
        // lexical: 1, 2, 3 — dense: 3, 1
        var fused = RrfFusion.Fuse([[1, 2, 3], [3, 1]], k: 60);

        fused.Select(item => item.Item).ShouldBe([1, 3, 2]);
        fused[0].Score.ShouldBe(1.0 / 61 + 1.0 / 62, 1e-12);
        fused[1].Score.ShouldBe(1.0 / 63 + 1.0 / 61, 1e-12);
        fused[2].Score.ShouldBe(1.0 / 62, 1e-12);
    }
}
