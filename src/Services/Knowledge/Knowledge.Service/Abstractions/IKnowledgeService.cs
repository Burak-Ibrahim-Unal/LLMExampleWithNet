using Knowledge.Application.Contracts;
using Shared.Application.Common;

namespace Knowledge.Service.Abstractions;

public interface IKnowledgeService
{
    Task<ApiResult<AnswerDto>> AskAsync(string question, CancellationToken cancellationToken = default);

    Task<ApiResult<IngestionSummaryDto>> ReindexAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<List<DocumentSummaryDto>>> ListDocumentsAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<DocumentDetailDto>> GetDocumentAsync(string id, CancellationToken cancellationToken = default);

    Task<ApiResult<SearchResultDto>> SearchAsync(string query, int? topK, CancellationToken cancellationToken = default);

    Task<ApiResult<SystemStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default);
}
