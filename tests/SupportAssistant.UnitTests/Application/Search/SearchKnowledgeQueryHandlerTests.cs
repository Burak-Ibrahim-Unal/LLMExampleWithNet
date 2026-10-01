using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Application.Options;
using Knowledge.Application.Queries.SearchKnowledge;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Application.Common;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Search;

public sealed class SearchKnowledgeQueryHandlerTests
{
    private static KnowledgeIndex HybridIndex()
    {
        var index = new KnowledgeIndex(new FakeTextEmbedder { QueryVector = _ => [0f, 1f] }, Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);
        var document = new KnowledgeDocument("para", "para", "Para İadesi", "1.0", new DateOnly(2025, 1, 1), DocumentStatus.Active, DocumentCategory.Policy, null, "hash");
        document.AddChunk("Para İadesi", "Ücret 5 iş günü içinde kartınıza aktarılır.").SetEmbedding([0f, 1f], FakeTextEmbedder.DefaultModel);
        index.Rebuild([document]);
        return index;
    }

    private static Task<ApiResult<SearchResultDto>> SearchAsync(string? mode)
    {
        var index = HybridIndex();
        var handler = new SearchKnowledgeQueryHandler(index, new KnowledgeBusinessRules(index), Options.Create(new RetrievalOptions()));
        return handler.Handle(new SearchKnowledgeQuery("para iadesi", 3, mode), TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null, "hybrid")]
    [InlineData("hybrid", "hybrid")]
    [InlineData("lexical", "lexical")]
    [InlineData("LEXICAL", "lexical")]
    public async Task The_mode_parameter_chooses_between_bm25_only_and_hybrid_retrieval(string? mode, string expectedMode)
    {
        var result = await SearchAsync(mode);

        result.Data!.RetrievalMode.ShouldBe(expectedMode);
    }

    [Fact]
    public async Task An_unknown_mode_is_rejected()
    {
        var result = await SearchAsync("semantic");

        result.StatusCode.ShouldBe(400);
        result.Message.ShouldBe("mode yalnızca 'lexical' veya 'hybrid' olabilir.");
    }
}
