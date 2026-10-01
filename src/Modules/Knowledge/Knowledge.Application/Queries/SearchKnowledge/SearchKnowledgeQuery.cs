using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.SearchKnowledge;

/// <param name="TopK">Number of sections to return; the configured default when null.</param>
public sealed record SearchKnowledgeQuery(string Query, int? TopK) : IRequest<ApiResult<SearchResultDto>>;
