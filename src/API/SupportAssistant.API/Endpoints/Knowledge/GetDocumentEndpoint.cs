using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.Knowledge;

/// <summary><c>GET /v1/documents/{id}</c> isteğinin rota parametresi.</summary>
public sealed class GetDocumentRequest
{
    /// <summary>
    /// Dokümanın front matter kimliği (ör. <c>iade-politikasi-v2</c>); rotadaki <c>{id}</c> parçasından bağlanır. Teknik
    /// Guid değil, yanıt kaynaklarında <c>documentId</c> olarak görünen okunabilir kimlik kullanılır.
    /// </summary>
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// <c>GET /v1/documents/{id}</c>: bir doküman sürümünü meta verisi ve tüm bölümleriyle (dosyadaki sırayla) döndürür;
/// kimlik yoksa 404.
/// </summary>
/// <remarks>
/// Yanıtlardaki kaynaklar yalnızca ilgili bölümü ve kısa bir alıntıyı gösterir; bu uç nokta, kaynağın tam metnine
/// bakılmasını ve alıntının bağlamı içinde doğrulanmasını sağlar. İnce bir adaptördür.
/// </remarks>
public sealed class GetDocumentEndpoint(IKnowledgeService knowledgeService) : Endpoint<GetDocumentRequest, ApiResult<DocumentDetailDto>>
{
    /// <summary>
    /// Rotayı (<c>/v1/documents/{id}</c>), GET fiilini, anonim erişimi (kimlik doğrulama ödev kapsamı dışında) ve OpenAPI
    /// özetini tanımlar.
    /// </summary>
    public override void Configure()
    {
        Get("documents/{id}");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = "Bir dokümanı bölümleriyle birlikte döndürür.";
        });
    }

    /// <summary>Kimliği servise iletir ve zarfı kendi durum koduyla (200 veya 404) gönderir.</summary>
    public override async Task HandleAsync(GetDocumentRequest req, CancellationToken ct)
    {
        var result = await knowledgeService.GetDocumentAsync(req.Id, ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
