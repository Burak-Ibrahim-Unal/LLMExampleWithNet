using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.Knowledge;

/// <summary>
/// <c>POST /v1/documents/reindex</c>: <c>knowledge-base/</c> klasörünü yeniden okur, veritabanı ve arama indeksiyle
/// uzlaştırır; yalnızca değişen dokümanlar yeniden bölünür ve embed edilir.
/// </summary>
/// <remarks>
/// Bir doküman düzenlendiğinde uygulamayı yeniden başlatmadan indeksi güncellemeyi, açılıştaki ingestion başarısız
/// olduysa yeniden denemeyi ve embedding sunucusu o sırada kapalıysa eksik vektörleri sonradan tamamlamayı sağlar. Yanıt
/// eklenen, güncellenen, silinen ve değişmeyen doküman sayılarını ve yeniden embed edilen bölüm sayısını içerir.
/// Eşzamanlı çağrılar handler'da sıraya alınır. İnce bir adaptördür.
/// </remarks>
public sealed class ReindexEndpoint(IKnowledgeService knowledgeService) : EndpointWithoutRequest<ApiResult<IngestionSummaryDto>>
{
    /// <summary>
    /// Rotayı (<c>/v1/documents/reindex</c>), POST fiilini, anonim erişimi ve OpenAPI özetini tanımlar. Sunucu durumunu
    /// değiştirdiği için POST kullanılır. Anonim erişim ödev kapsamındaki bir sadeleştirmedir; gerçek bir kurulumda bu
    /// operatör işlemi yetkilendirme arkasında olmalıdır (README, bilinen sınırlar).
    /// </summary>
    public override void Configure()
    {
        Post("documents/reindex");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = "knowledge-base/ klasörünü yeniden okur; yalnızca değişen dokümanlar yeniden embed edilir.";
        });
    }

    /// <summary>
    /// Yeniden indekslemeyi başlatır ve özeti kendi durum koduyla gönderir: başarıda 200; bilgi tabanı boş, okunamaz veya
    /// geçersizse 422.
    /// </summary>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await knowledgeService.ReindexAsync(ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
