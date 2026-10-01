using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.System;

public sealed class HealthEndpoint(IKnowledgeService knowledgeService) : EndpointWithoutRequest<ApiResult<SystemStatusDto>>
{
    public override void Configure()
    {
        Get("health");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = "İndeks, dil modeli ve embedding yapılandırmasının durumunu döndürür.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await knowledgeService.GetStatusAsync(ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
