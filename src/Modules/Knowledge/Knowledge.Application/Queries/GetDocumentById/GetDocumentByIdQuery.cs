using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetDocumentById;

public sealed record GetDocumentByIdQuery(string Id) : IRequest<ApiResult<DocumentDetailDto>>;
