using FastEndpoints;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.System;

public sealed record HealthResponse(string Status);

public sealed class HealthEndpoint : EndpointWithoutRequest<ApiResult<HealthResponse>>
{
    public override void Configure()
    {
        Get("health");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = ApiResult<HealthResponse>.Ok(new HealthResponse("ok"));
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
