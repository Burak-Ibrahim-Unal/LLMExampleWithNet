using Knowledge.Application.Abstractions;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Application.Options;
using MediatR;
using Microsoft.Extensions.Options;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.SearchKnowledge;

/// <summary>Retrieval only, without the language model: shows what the assistant would read for a question.</summary>
public sealed class SearchKnowledgeQueryHandler(
    IKnowledgeIndex index,
    KnowledgeBusinessRules rules,
    IOptions<RetrievalOptions> options) : IRequestHandler<SearchKnowledgeQuery, ApiResult<SearchResultDto>>
{
    public async Task<ApiResult<SearchResultDto>> Handle(SearchKnowledgeQuery request, CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim() ?? string.Empty;
        var topK = request.TopK ?? options.Value.TopK;

        var requiredError = rules.CheckQueryRequired<SearchResultDto>(query);
        if (requiredError is not null)
        {
            return requiredError;
        }

        var lengthError = rules.CheckQueryLength<SearchResultDto>(query);
        if (lengthError is not null)
        {
            return lengthError;
        }

        var topKError = rules.CheckTopKInRange<SearchResultDto>(topK);
        if (topKError is not null)
        {
            return topKError;
        }

        var modeError = rules.CheckRetrievalMode<SearchResultDto>(request.Mode);
        if (modeError is not null)
        {
            return modeError;
        }

        var readyError = rules.CheckIndexReady<SearchResultDto>();
        if (readyError is not null)
        {
            return readyError;
        }

        var prepared = await index.PrepareAsync(query, cancellationToken);

        // Without a query vector the index ranks with BM25 only — used to measure what vectors add.
        if (string.Equals(request.Mode, "lexical", StringComparison.OrdinalIgnoreCase))
        {
            prepared = prepared with { Vector = null };
        }

        var result = index.Search(prepared, topK);

        var hits = result.Hits
            .Select(hit => new SearchHitDto(
                hit.Chunk.DocumentId,
                hit.Chunk.Title,
                hit.Chunk.Version,
                hit.Chunk.EffectiveDate,
                hit.Chunk.Status.ToApi(),
                hit.Chunk.Category.ToApi(),
                hit.Chunk.SectionPath,
                hit.Chunk.Content,
                hit.FusedScore,
                hit.LexicalScore,
                hit.LexicalCoverage,
                hit.DenseScore))
            .ToList();

        return ApiResult<SearchResultDto>.Ok(new SearchResultDto(query, result.Mode.ToApi(), result.MaxDenseScore, result.MaxLexicalCoverage, hits));
    }
}
