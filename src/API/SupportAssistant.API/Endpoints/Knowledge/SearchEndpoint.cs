using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.Knowledge;

/// <summary>
/// <c>GET /v1/search</c> isteğinin sorgu dizesi parametreleri: <c>?q=...&amp;topK=...&amp;mode=...</c>.
/// </summary>
public sealed class SearchRequest
{
    /// <summary>
    /// Arama metni (<c>q</c>). Parametre hiç gönderilmeyebileceği için nullable'dır; boşsa iş kuralı 400 döndürür.
    /// </summary>
    public string? Q { get; set; }

    /// <summary>
    /// Döndürülecek bölüm sayısı (1–20); verilmezse yapılandırmadaki varsayılan (<c>Retrieval:TopK</c>, varsayılan 8)
    /// kullanılır.
    /// </summary>
    public int? TopK { get; set; }

    /// <summary>
    /// "lexical" yalnızca BM25, "hybrid" (varsayılan) ise mümkünse vektör benzerliğini de ekler. Embedding kullanılamıyorsa
    /// hybrid istek de BM25 ile yanıtlanır; gerçekte kullanılan mod yanıttaki <c>retrievalMode</c> alanındadır.
    /// </summary>
    public string? Mode { get; set; }
}

/// <summary>
/// <c>GET /v1/search</c>: dil modeli olmadan yalnızca arama yapar; bir soru için hangi bölümlerin bulunduğunu ve
/// skorlarını (RRF, BM25, kelime kapsamı, kosinüs benzerliği) gösterir.
/// </summary>
/// <remarks>
/// Aramayı cevap üretiminden ayrı gözlemlemek için vardır: değerlendirme aracı bu uç noktayla arama isabetini
/// <c>lexical</c> ve <c>hybrid</c> modlarda ayrı ayrı ölçer ve bir hatanın aramadan mı yoksa cevap üretiminden mi
/// geldiğini ayırt eder. Sürüm çözümü uygulanmaz; eski sürümlerin bölümleri de durumlarıyla birlikte görünür. Uç nokta
/// ince bir adaptördür.
/// </remarks>
public sealed class SearchEndpoint(IKnowledgeService knowledgeService) : Endpoint<SearchRequest, ApiResult<SearchResultDto>>
{
    /// <summary>
    /// Rotayı (<c>/v1/search</c>), GET fiilini, anonim erişimi ve OpenAPI özetini tanımlar. İşlem salt okuma olduğu ve
    /// parametreler sorgu dizesinde taşındığı için GET kullanılır; bir arama URL'si paylaşılıp tekrar çalıştırılabilir.
    /// Kimlik doğrulama ödev kapsamı dışında olduğundan erişim anonimdir.
    /// </summary>
    public override void Configure()
    {
        Get("search");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = "Dil modeli olmadan arama: bir soru için hangi bölümlerin bulunduğunu ve skorlarını gösterir.";
        });
    }

    /// <summary>
    /// Parametreleri servise iletir ve zarfı kendi durum koduyla gönderir. Eksik <c>q</c> boş metne çevrilir; böylece
    /// "boş arama" hatası da iş kuralından, aynı zarf ve aynı Türkçe mesajla döner.
    /// </summary>
    public override async Task HandleAsync(SearchRequest req, CancellationToken ct)
    {
        var result = await knowledgeService.SearchAsync(req.Q ?? string.Empty, req.TopK, req.Mode, ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
