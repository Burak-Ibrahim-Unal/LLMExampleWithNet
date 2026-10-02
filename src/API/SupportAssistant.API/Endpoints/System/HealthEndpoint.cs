using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.System;

/// <summary>
/// <c>GET /v1/health</c>: indeksin (hazır mı, kaç doküman ve bölüm, hangi arama modu), dil modelinin ve embedding
/// yapılandırmasının durumunu döndürür.
/// </summary>
/// <remarks>
/// Diğer uç noktalar gibi ince bir adaptördür: isteği <c>IKnowledgeService</c>'e iletir, dönen <c>ApiResult</c> zarfını
/// kendi durum koduyla gönderir. Yanıt her zaman 200'dür; genel durum gövdede <c>ok</c> (indeks hazır ve dil modeli
/// yapılandırılmış) veya <c>degraded</c> olarak bildirilir. Böylece açılıştaki ingestion başarısız olsa ya da dil modeli
/// yapılandırılmamış olsa bile hangi bileşenin eksik olduğu görülebilir.
/// </remarks>
public sealed class HealthEndpoint(IKnowledgeService knowledgeService) : EndpointWithoutRequest<ApiResult<SystemStatusDto>>
{
    /// <summary>
    /// Rotayı (<c>health</c>; genel <c>v1</c> önekiyle <c>/v1/health</c>), GET fiilini, anonim erişimi ve OpenAPI özetini
    /// tanımlar. FastEndpoints uç noktaları varsayılan olarak kimlik doğrulama ister; bu API'de kimlik doğrulama ödev
    /// kapsamı dışında olduğundan erişim açıkça anonim yapılır. Özet metni OpenAPI belgesinde ve Scalar arayüzünde görünür.
    /// </summary>
    public override void Configure()
    {
        Get("health");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = "İndeks, dil modeli ve embedding yapılandırmasının durumunu döndürür.";
        });
    }

    /// <summary>Durumu servisten alır ve zarfı, içindeki durum koduyla aynen gönderir.</summary>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await knowledgeService.GetStatusAsync(ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
