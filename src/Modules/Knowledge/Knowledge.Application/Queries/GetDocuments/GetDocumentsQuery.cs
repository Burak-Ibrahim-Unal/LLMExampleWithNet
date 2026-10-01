using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetDocuments;

public sealed record GetDocumentsQuery : IRequest<ApiResult<List<DocumentSummaryDto>>>;
