using Knowledge.Application.Commands.AskQuestion;
using Knowledge.Application.Commands.IngestKnowledgeBase;
using Knowledge.Application.Contracts;
using Knowledge.Application.Queries.GetDocumentById;
using Knowledge.Application.Queries.GetDocuments;
using Knowledge.Application.Queries.GetSystemStatus;
using Knowledge.Application.Queries.SearchKnowledge;
using Knowledge.Service.Abstractions;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Service;

public sealed class KnowledgeService(ISender sender) : IKnowledgeService
{
    public Task<ApiResult<AnswerDto>> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        return sender.Send(new AskQuestionCommand(question), cancellationToken);
    }

    public Task<ApiResult<IngestionSummaryDto>> ReindexAsync(CancellationToken cancellationToken = default)
    {
        return sender.Send(new IngestKnowledgeBaseCommand(), cancellationToken);
    }

    public Task<ApiResult<List<DocumentSummaryDto>>> ListDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return sender.Send(new GetDocumentsQuery(), cancellationToken);
    }

    public Task<ApiResult<DocumentDetailDto>> GetDocumentAsync(string id, CancellationToken cancellationToken = default)
    {
        return sender.Send(new GetDocumentByIdQuery(id), cancellationToken);
    }

    public Task<ApiResult<SearchResultDto>> SearchAsync(string query, int? topK, string? mode = null, CancellationToken cancellationToken = default)
    {
        return sender.Send(new SearchKnowledgeQuery(query, topK, mode), cancellationToken);
    }

    public Task<ApiResult<SystemStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return sender.Send(new GetSystemStatusQuery(), cancellationToken);
    }
}
