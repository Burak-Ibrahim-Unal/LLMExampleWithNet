using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.SearchKnowledge;

/// <param name="TopK">Number of sections to return; the configured default when null.</param>
/// <param name="Mode">"lexical" forces BM25-only retrieval, "hybrid" (or null) uses vectors when the index has them.</param>
public sealed record SearchKnowledgeQuery(string Query, int? TopK, string? Mode = null) : IRequest<ApiResult<SearchResultDto>>;
