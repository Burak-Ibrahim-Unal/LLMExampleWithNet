using Knowledge.Application.Abstractions;
using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetSystemStatus;

public sealed class GetSystemStatusQueryHandler(IKnowledgeIndex index, IGroundedAnswerGenerator generator, ITextEmbedder embedder)
    : IRequestHandler<GetSystemStatusQuery, ApiResult<SystemStatusDto>>
{
    public Task<ApiResult<SystemStatusDto>> Handle(GetSystemStatusQuery request, CancellationToken cancellationToken)
    {
        var indexStatus = index.Status;

        var status = new SystemStatusDto(
            indexStatus.IsReady && generator.IsConfigured ? "ok" : "degraded",
            new IndexStatusDto(indexStatus.IsReady, indexStatus.DocumentCount, indexStatus.ChunkCount, indexStatus.Mode.ToApi(), indexStatus.BuiltAtUtc),
            new ComponentStatusDto(generator.IsConfigured, generator.ModelName),
            new ComponentStatusDto(embedder.IsEnabled, embedder.ModelName));

        return Task.FromResult(ApiResult<SystemStatusDto>.Ok(status));
    }
}
