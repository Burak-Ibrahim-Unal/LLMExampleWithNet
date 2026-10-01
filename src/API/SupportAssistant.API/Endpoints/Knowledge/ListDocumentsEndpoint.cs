using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.Knowledge;

public sealed class ListDocumentsEndpoint(IKnowledgeService knowledgeService) : EndpointWithoutRequest<ApiResult<List<DocumentSummaryDto>>>
{
    public override void Configure()
    {
        Get("documents");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = "Bilgi tabanındaki dokümanları sürüm bilgileriyle listeler.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await knowledgeService.ListDocumentsAsync(ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
