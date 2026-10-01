using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Domain.Repositories;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetDocumentById;

public sealed class GetDocumentByIdQueryHandler(IKnowledgeDocumentRepository repository, KnowledgeBusinessRules rules)
    : IRequestHandler<GetDocumentByIdQuery, ApiResult<DocumentDetailDto>>
{
    public async Task<ApiResult<DocumentDetailDto>> Handle(GetDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        var document = await repository.GetBySourceIdWithChunksAsync(request.Id.Trim(), cancellationToken);

        var notFoundError = rules.CheckDocumentFound<DocumentDetailDto>(document);
        if (notFoundError is not null)
        {
            return notFoundError;
        }

        var sections = document!.Chunks
            .OrderBy(chunk => chunk.Order)
            .Select(chunk => new DocumentSectionDto(chunk.Order, chunk.SectionPath, chunk.Content))
            .ToList();

        var payload = new DocumentDetailDto(
            document.SourceId,
            document.DocumentKey,
            document.Title,
            document.Version,
            document.EffectiveDate,
            document.Status.ToApi(),
            document.Category.ToApi(),
            document.Supersedes,
            sections);

        return ApiResult<DocumentDetailDto>.Ok(payload);
    }
}
