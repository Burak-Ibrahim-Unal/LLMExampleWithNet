using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetDocuments;

/// <summary>
/// Bilgi tabanındaki tüm dokümanları, her sürüm ayrı bir kayıt olacak şekilde özet bilgileriyle listeleme isteği;
/// <c>GET /v1/documents</c> uç noktasının MediatR karşılığıdır. Eski ve güncel sürümler birlikte listelenir; böylece bir
/// prosedürün hangi sürümünün yürürlükte olduğu (<c>status</c>, <c>effectiveDate</c>, <c>supersedes</c>) API üzerinden
/// görülebilir. Bilgi tabanı on civarında dokümandan oluştuğu için filtre ya da sayfalama parametresi yoktur.
/// </summary>
public sealed record GetDocumentsQuery : IRequest<ApiResult<List<DocumentSummaryDto>>>;
