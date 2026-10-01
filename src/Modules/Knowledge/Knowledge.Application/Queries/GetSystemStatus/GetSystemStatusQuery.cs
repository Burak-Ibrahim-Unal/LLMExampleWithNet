using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetSystemStatus;

public sealed record GetSystemStatusQuery : IRequest<ApiResult<SystemStatusDto>>;
