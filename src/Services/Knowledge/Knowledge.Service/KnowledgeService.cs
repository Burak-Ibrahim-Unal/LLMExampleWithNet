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

/// <summary>
/// <see cref="IKnowledgeService"/>'in MediatR tabanlı uygulaması: her metot ilgili komut veya sorguyu oluşturup
/// <c>ISender</c> ile handler'ına gönderir.
/// </summary>
/// <remarks>
/// İnce bir cephedir: iş mantığı handler'larda ve iş kurallarında durur, burada yalnızca metot çağrısından mesaja eşleme
/// yapılır. Komut/sorgu ayrımı (CQRS) burada da görünür: yan etkisi olan işlemler komuttur (soru: dil modeli çağrısı ve
/// denetim kaydı; reindex: veritabanı yazımı), diğerleri sorgudur. Scoped kaydedilir; böylece handler'lar istek
/// scope'undaki <c>AppDbContext</c>'i kullanır.
/// </remarks>
public sealed class KnowledgeService(ISender sender) : IKnowledgeService
{
    /// <summary>Soruyu <c>AskQuestionCommand</c> olarak gönderir; doğrulama ve tüm yanıt hattı handler'dadır.</summary>
    public Task<ApiResult<AnswerDto>> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        return sender.Send(new AskQuestionCommand(question), cancellationToken);
    }

    /// <summary>
    /// <c>IngestKnowledgeBaseCommand</c> gönderir. Eşzamanlı çağrılar handler'daki statik semafor ile sıraya girer; böylece
    /// açılıştaki ingestion ile bir reindex isteği aynı satırlar üzerinde yarışmaz.
    /// </summary>
    public Task<ApiResult<IngestionSummaryDto>> ReindexAsync(CancellationToken cancellationToken = default)
    {
        return sender.Send(new IngestKnowledgeBaseCommand(), cancellationToken);
    }

    /// <summary>Doküman listesini <c>GetDocumentsQuery</c> ile ister.</summary>
    public Task<ApiResult<List<DocumentSummaryDto>>> ListDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return sender.Send(new GetDocumentsQuery(), cancellationToken);
    }

    /// <summary>Tek dokümanı <c>GetDocumentByIdQuery</c> ile ister; kimlik handler'da kırpılır.</summary>
    public Task<ApiResult<DocumentDetailDto>> GetDocumentAsync(string id, CancellationToken cancellationToken = default)
    {
        return sender.Send(new GetDocumentByIdQuery(id), cancellationToken);
    }

    /// <summary>
    /// Aramayı <c>SearchKnowledgeQuery</c> ile ister; <c>topK</c> ve <c>mode</c> doğrulaması ile varsayılanlar handler'da
    /// uygulanır.
    /// </summary>
    public Task<ApiResult<SearchResultDto>> SearchAsync(string query, int? topK, string? mode = null, CancellationToken cancellationToken = default)
    {
        return sender.Send(new SearchKnowledgeQuery(query, topK, mode), cancellationToken);
    }

    /// <summary>Sistem durumunu <c>GetSystemStatusQuery</c> ile ister.</summary>
    public Task<ApiResult<SystemStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return sender.Send(new GetSystemStatusQuery(), cancellationToken);
    }
}
