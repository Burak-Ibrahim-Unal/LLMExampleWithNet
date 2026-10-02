using Knowledge.Application.Contracts;
using Knowledge.Domain.Repositories;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetDocuments;

/// <summary>
/// <see cref="GetDocumentsQuery"/> isteğini işler: veritabanındaki tüm doküman sürümlerini kimliğe (<c>SourceId</c>)
/// göre sıralı olarak okur ve her biri için bir <see cref="DocumentSummaryDto"/> satırı üretir. Arama indeksinden
/// bağımsızdır; indeks hazır olmasa bile hangi dokümanların kayıtlı olduğu görülebilir.
/// </summary>
public sealed class GetDocumentsQueryHandler(IKnowledgeDocumentRepository repository)
    : IRequestHandler<GetDocumentsQuery, ApiResult<List<DocumentSummaryDto>>>
{
    /// <summary>
    /// Dokümanları bölümleriyle birlikte yükler ve özet satırlarına çevirir. Bölümlerin içeriği değil yalnızca sayısı
    /// (<c>SectionCount</c>) döner; tam metin için <c>GET /v1/documents/{id}</c> kullanılır. Durum ve tür, front
    /// matter'daki sözcüklerle ("active"/"superseded", "politika"/"prosedur"/"kilavuz"/"sss") döndürülür; böylece API
    /// çıktısı dokümanları yazanların kullandığı sözcüklerle aynı kalır.
    /// </summary>
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
