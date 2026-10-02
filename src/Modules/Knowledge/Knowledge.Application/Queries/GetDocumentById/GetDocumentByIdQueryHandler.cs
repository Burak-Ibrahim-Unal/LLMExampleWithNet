using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Domain.Repositories;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetDocumentById;

/// <summary>
/// <see cref="GetDocumentByIdQuery"/> isteğini işler: dokümanı bölümleriyle birlikte veritabanından okur ve API
/// biçimine çevirir; bulunamazsa iş kuralı (<c>CheckDocumentFound</c>) 404 döndürür.
/// </summary>
/// <remarks>
/// Arama indeksinden değil veritabanından okunur: bu uç nokta arama yapmaz, saklanan dokümanın kendisini gösterir ve
/// indeks henüz hazır olmasa bile (ör. açılıştaki indeksleme başarısız olduysa) çalışır.
/// </remarks>
public sealed class GetDocumentByIdQueryHandler(IKnowledgeDocumentRepository repository, KnowledgeBusinessRules rules)
    : IRequestHandler<GetDocumentByIdQuery, ApiResult<DocumentDetailDto>>
{
    /// <summary>
    /// Kimliği kırparak dokümanı bölümleriyle yükler; yoksa 404 döner, varsa bölümleri dosyadaki sırasına
    /// (<c>Order</c>) göre dizip <see cref="DocumentDetailDto"/> olarak döndürür. Sıralama açıkça yapılır, çünkü
    /// veritabanından yüklenen alt koleksiyonun sırası garanti değildir; okuyan kişi bölümleri dosyadaki akışla
    /// görmelidir. Durum ve tür, front matter'daki sözcüklerle ("active", "politika" gibi) döndürülür.
    /// </summary>
    public async Task<ApiResult<DocumentDetailDto>> Handle(GetDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        var document = await repository.GetBySourceIdWithChunksAsync(request.Id.Trim(), cancellationToken);

        var notFoundError = rules.CheckDocumentFound<DocumentDetailDto>(document);
        if (notFoundError is not null)
        {
            return notFoundError;
        }

        // Kural null'ı yukarıda eledi; derleyici bunu kural metodunun içinden göremediği için null-forgiving (!) gerekir.
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
