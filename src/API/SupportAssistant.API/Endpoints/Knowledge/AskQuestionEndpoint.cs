using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;
using SupportAssistant.API.Security;

namespace SupportAssistant.API.Endpoints.Knowledge;

/// <summary>
/// <c>POST /v1/questions</c> isteğinin JSON gövdesi: <c>{ "question": "..." }</c>.
/// </summary>
public sealed class AskQuestionRequest
{
    /// <summary>
    /// Türkçe destek sorusu; en fazla 500 karakter. Boşluk ve uzunluk denetimi burada değil Application katmanındaki iş
    /// kurallarında yapılır; böylece hata yanıtı da aynı <c>ApiResult</c> zarfında ve merkezi Türkçe mesajla döner.
    /// </summary>
    public string Question { get; set; } = string.Empty;
}

/// <summary>
/// <c>POST /v1/questions</c>: soruyu yalnızca bilgi tabanındaki dokümanlara dayanarak yanıtlar.
/// </summary>
/// <remarks>
/// Projenin ana uç noktasıdır. Yanıt; kullanılan doküman, sürüm, bölüm ve alıntıyı, alıntının bölüm metninde doğrulanıp
/// doğrulanmadığıyla (<c>quoteVerified</c>) birlikte (<c>sources</c>), eski
/// sürümlerin nasıl elendiğini (<c>versionResolution</c>) ve kaynaklar arası çelişkileri (<c>conflicts</c>) içerir.
/// Dokümanlarda yeterli bilgi yoksa 200 ile <c>answerable=false</c>, sabit bir Türkçe mesaj ve <c>refusalReason</c>
/// döner. Uç nokta ince bir adaptördür: tüm yanıt hattı (arama, kapılar, sürüm çözümü, dil modeli, atıf doğrulaması)
/// Application katmanındaki handler'dadır; bu sayede aynı mantık HTTP olmadan birim testleriyle sınanabilir.
/// </remarks>
public sealed class AskQuestionEndpoint(IKnowledgeService knowledgeService) : Endpoint<AskQuestionRequest, ApiResult<AnswerDto>>
{
    /// <summary>
    /// Rotayı (<c>/v1/questions</c>), POST fiilini, anonim erişimi, hız sınırı politikasını ve OpenAPI
    /// özet/açıklamasını tanımlar. Soru gövdede taşındığı ve her çağrı dil modelini çağırıp bir denetim kaydı
    /// yazabildiği için POST kullanılır. Kimlik doğrulama ödev kapsamı dışında olduğundan erişim anonimdir; bu yüzden uç,
    /// istemci IP'si başına dakikalık bir sınırla korunur (<see cref="RateLimitingOptions"/>). Açıklama, yanıtın
    /// alanlarını Scalar arayüzünde Türkçe anlatır.
    /// </summary>
    public override void Configure()
    {
        Post("questions");
        AllowAnonymous();
        Options(endpoint => endpoint.RequireRateLimiting(RateLimitingOptions.QuestionsPolicy));
        Summary(summary =>
        {
            summary.Summary = "Soruyu bilgi tabanındaki dokümanlara dayanarak yanıtlar.";
            summary.Description =
                "Yanıt, kullanılan doküman/sürüm/bölümü ve alıntıyı (sources), eski sürümlerin nasıl elendiğini (versionResolution) " +
                "ve kaynaklar arası çelişkileri (conflicts) içerir. Dokümanlarda yeterli bilgi yoksa answerable=false döner.";
        });
    }

    /// <summary>
    /// Soruyu servise iletir ve zarfı kendi durum koduyla gönderir: 200 yanıt veya geri çevirme, 400 geçersiz soru,
    /// 502/503 dil modeli ya da hazır olmayan indeks.
    /// </summary>
    public override async Task HandleAsync(AskQuestionRequest req, CancellationToken ct)
    {
        var result = await knowledgeService.AskAsync(req.Question, ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
