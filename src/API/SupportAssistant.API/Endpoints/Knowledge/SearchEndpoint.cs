using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.Knowledge;

public sealed class SearchRequest
{
    /// <summary>Search text.</summary>
    public string? Q { get; set; }

    /// <summary>Number of sections to return (1–20); the configured default when omitted.</summary>
    public int? TopK { get; set; }
}

public sealed class SearchEndpoint(IKnowledgeService knowledgeService) : Endpoint<SearchRequest, ApiResult<SearchResultDto>>
{
    public override void Configure()
    {
        Get("search");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = "Dil modeli olmadan arama: bir soru için hangi bölümlerin bulunduğunu ve skorlarını gösterir.";
        });
    }

    public override async Task HandleAsync(SearchRequest req, CancellationToken ct)
    {
        var result = await knowledgeService.SearchAsync(req.Q ?? string.Empty, req.TopK, ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
