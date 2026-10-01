using Knowledge.Infrastructure.Search;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Search;

public sealed class Bm25IndexTests
{
    // 0: return period, 1: shipping fee, 2: return shipping fee
    private static readonly IReadOnlyList<IReadOnlyList<string>> Documents =
    [
        ["iade", "sures", "30", "gun"],
        ["kargo", "ucret", "750", "tl"],
        ["iade", "kargo", "ucret", "lumor"]
    ];

    [Fact]
    public void Score_ranks_the_document_containing_every_query_term_first()
    {
        var index = new Bm25Index(Documents);

        index.Score(["iade", "kargo"])[0].DocumentIndex.ShouldBe(2);
        index.Score(["iade", "sures"])[0].DocumentIndex.ShouldBe(0);
    }

    [Fact]
    public void Score_returns_only_documents_sharing_a_term_with_the_query()
    {
        var index = new Bm25Index(Documents);

        index.Score(["homek"]).ShouldBeEmpty();
        index.Score(["sures"]).Select(match => match.DocumentIndex).ShouldBe([0]);
    }

    [Fact]
    public void Coverage_is_the_idf_weighted_share_of_query_terms_found_in_the_document()
    {
        var index = new Bm25Index(Documents);

        index.Coverage(["iade", "sures"], 0).ShouldBe(1.0, 1e-9);

        // idf(iade) = ln(1 + 1.5 / 2.5) = 0.470004; unseen idf(homek) = ln(1 + 3.5 / 0.5) = 2.079442
        index.Coverage(["iade", "homek"], 0).ShouldBe(0.184355, 1e-5);
    }
}
