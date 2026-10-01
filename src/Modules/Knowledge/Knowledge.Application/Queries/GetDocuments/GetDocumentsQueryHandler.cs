using Knowledge.Application.Contracts;
using Knowledge.Domain.Repositories;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetDocuments;

public sealed class GetDocumentsQueryHandler(IKnowledgeDocumentRepository repository)
    : IRequestHandler<GetDocumentsQuery, ApiResult<List<DocumentSummaryDto>>>
{
    public async Task<ApiResult<List<DocumentSummaryDto>>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        var documents = await repository.ListWithChunksAsync(cancellationToken);

        var payload = documents
            .Select(document => new DocumentSummaryDto(
                document.SourceId,
                document.DocumentKey,
                document.Title,
                document.Version,
                document.EffectiveDate,
                document.Status.ToApi(),
                document.Category.ToApi(),
                document.Supersedes,
                document.Chunks.Count))
            .ToList();

        return ApiResult<List<DocumentSummaryDto>>.Ok(payload);
    }
}
