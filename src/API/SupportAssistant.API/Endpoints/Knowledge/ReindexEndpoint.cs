using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;
using SupportAssistant.API.Security;

namespace SupportAssistant.API.Endpoints.Knowledge;

/// <summary>
/// <c>POST /v1/documents/reindex</c>: <c>knowledge-base/</c> klasörünü yeniden okur, veritabanı ve arama indeksiyle
/// uzlaştırır; yalnızca değişen dokümanlar yeniden bölünür ve embed edilir.
/// </summary>
/// <remarks>
/// Bir doküman düzenlendiğinde uygulamayı yeniden başlatmadan indeksi güncellemeyi, açılıştaki ingestion başarısız
/// olduysa yeniden denemeyi ve embedding sunucusu o sırada kapalıysa eksik vektörleri sonradan tamamlamayı sağlar. Yanıt
/// eklenen, güncellenen, silinen ve değişmeyen doküman sayılarını, yeniden embed edilen bölüm sayısını ve talimat
/// benzeri metin içeren (şüpheli) dokümanları içerir. Eşzamanlı çağrılar handler'da sıraya alınır. İnce bir adaptördür;
/// erişim denetimi <see cref="AdminKeyPreProcessor"/>'dadır.
/// </remarks>
public sealed class ReindexEndpoint(IKnowledgeService knowledgeService) : EndpointWithoutRequest<ApiResult<IngestionSummaryDto>>
{
    /// <summary>
    /// Rotayı (<c>/v1/documents/reindex</c>), POST fiilini, yönetici anahtarı denetimini ve OpenAPI özetini tanımlar.
    /// Sunucu durumunu değiştirdiği için POST kullanılır. Uygulamada kullanıcı kimliği olmadığından ASP.NET Core
    /// yetkilendirmesi açısından uç anonimdir; bunun yerine <see cref="AdminKeyPreProcessor"/> <c>X-Admin-Key</c>
    /// başlığını <c>Security__AdminApiKey</c> ile karşılaştırır. Anahtar tanımlı değilse uç kapalıdır (403).
    /// </summary>
    public override void Configure()
    {
        Post("documents/reindex");
        AllowAnonymous();
        PreProcessor<AdminKeyPreProcessor>();
        Summary(summary =>
        {
            summary.Summary = "knowledge-base/ klasörünü yeniden okur; yalnızca değişen dokümanlar yeniden embed edilir.";
            summary.Description =
                "Yönetici işlemidir: X-Admin-Key başlığında sunucudaki Security__AdminApiKey değeri gönderilmelidir. " +
                "Başlık eksik ya da yanlışsa 401, sunucuda anahtar tanımlı değilse 403 döner.";
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
