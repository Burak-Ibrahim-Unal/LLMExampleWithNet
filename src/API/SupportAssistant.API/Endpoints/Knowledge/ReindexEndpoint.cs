using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.Knowledge;

public sealed class ReindexEndpoint(IKnowledgeService knowledgeService) : EndpointWithoutRequest<ApiResult<IngestionSummaryDto>>
{
    public override void Configure()
    {
        Post("documents/reindex");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = "knowledge-base/ klasörünü yeniden okur; yalnızca değişen dokümanlar yeniden embed edilir.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await knowledgeService.ReindexAsync(ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
