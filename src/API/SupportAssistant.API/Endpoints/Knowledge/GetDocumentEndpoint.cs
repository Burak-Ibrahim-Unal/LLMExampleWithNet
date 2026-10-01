using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.Knowledge;

public sealed class GetDocumentRequest
{
    public string Id { get; set; } = string.Empty;
}

public sealed class GetDocumentEndpoint(IKnowledgeService knowledgeService) : Endpoint<GetDocumentRequest, ApiResult<DocumentDetailDto>>
{
    public override void Configure()
    {
        Get("documents/{id}");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = "Bir dokümanı bölümleriyle birlikte döndürür.";
        });
    }

    public override async Task HandleAsync(GetDocumentRequest req, CancellationToken ct)
    {
        var result = await knowledgeService.GetDocumentAsync(req.Id, ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
