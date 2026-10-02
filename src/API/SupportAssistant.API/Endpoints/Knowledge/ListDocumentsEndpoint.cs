using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.Knowledge;

/// <summary>
/// <c>GET /v1/documents</c>: bilgi tabanındaki tüm doküman sürümlerini (superseded olanlar dahil) sürüm bilgileri
/// (<c>documentKey</c>, sürüm, yürürlük tarihi, durum, tür, <c>supersedes</c>) ve bölüm sayılarıyla listeler.
/// </summary>
/// <remarks>
/// Sürüm geçmişini görünür kılar: hangi prosedürün eski ve güncel sürümü olduğu, yanıtlardaki sürüm kararlarıyla
/// karşılaştırılabilir. Veriyi arama indeksinden değil veritabanından okur. İnce bir adaptördür.
/// </remarks>
public sealed class ListDocumentsEndpoint(IKnowledgeService knowledgeService) : EndpointWithoutRequest<ApiResult<List<DocumentSummaryDto>>>
{
    /// <summary>
    /// Rotayı (<c>/v1/documents</c>), GET fiilini, anonim erişimi (kimlik doğrulama ödev kapsamı dışında) ve OpenAPI
    /// özetini tanımlar.
    /// </summary>
    public override void Configure()
    {
        Get("documents");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = Messages.ApiDocs.ListDocumentsSummary;
        });
    }

    /// <summary>Listeyi servisten alır ve zarfı kendi durum koduyla gönderir.</summary>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await knowledgeService.ListDocumentsAsync(ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
