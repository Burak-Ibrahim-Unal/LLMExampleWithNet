using Knowledge.Application.Abstractions;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Application.Options;
using MediatR;
using Microsoft.Extensions.Options;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.SearchKnowledge;

/// <summary>
/// Dil modeli olmadan yalnızca arama: bir soru için asistanın okuyacağı bölüm adaylarını; sıralamayı belirleyen RRF
/// skoru, BM25 skoru, sözcük kapsamı ve kosinüs benzerliğiyle birlikte döndürür. Sürüm çözümlemesi uygulanmaz: eski
/// sürümler de durumlarıyla (<c>status</c>) birlikte listelenir, çünkü amaç ham arama sonucunu görmektir.
/// </summary>
/// <remarks>
/// LLM'siz bir arama uç noktasının iki gerekçesi var. Hata ayıklama: bir yanıt yanlışsa ya da reddedildiyse önce doğru
/// bölümün bulunup bulunmadığına ve Kapı 1'in baktığı sinyallere (en iyi kosinüs, en iyi sözcük kapsamı) bakılabilir.
/// Değerlendirme: aramanın, yanıt üretiminden ayrı ölçülmesi gerekir; değerlendirme aracı her soruyu <c>mode=lexical</c>
/// ve <c>mode=hybrid</c> ile ayrıca arayarak bir hatanın aramadan mı yoksa modelden mi kaynaklandığını ayırt eder ve
/// vektörlerin ne kattığını gösterir (son raporda beklenen kaynak, yalnızca BM25 ile 12 sorunun 10'unda, hibrit
/// aramayla 12'sinin tamamında bulundu). Model çağrılmadığı için ucuzdur ve dil modeli yapılandırılmamış olsa bile
/// çalışır.
/// </remarks>
public sealed class SearchKnowledgeQueryHandler(
    IKnowledgeIndex index,
    KnowledgeBusinessRules rules,
    IOptions<RetrievalOptions> options) : IRequestHandler<SearchKnowledgeQuery, ApiResult<SearchResultDto>>
{
    /// <summary>
    /// İsteği doğrular (sorgu boş olamaz, en fazla 500 karakter, <c>topK</c> 1–20 arası, <c>mode</c> yalnızca
    /// lexical/hybrid, indeks hazır), sorguyu hazırlar ve aramayı çalıştırır. Doğrulama hataları 400, hazır olmayan
    /// indeks 503 döner; <c>topK</c> verilmezse soru hattıyla aynı varsayılan (<c>Retrieval:TopK</c>) kullanılır.
    /// </summary>
    /// <remarks>
    /// <c>mode=lexical</c> verildiğinde hazırlanmış sorgunun vektörü atılır (<c>Vector = null</c>); vektörü olmayan bir
    /// sorguda indeks yalnızca BM25 sıralamasını kullanır ve sonucu <c>lexical</c> olarak işaretler. Böylece mod için
    /// ayrı bir arama yolu gerekmez: soru hattının kullandığı arama kodu, embedding sunucusu düştüğünde olacağı gibi
    /// vektörsüz çalıştırılmış olur. <c>hybrid</c> ise bir istektir, garanti değildir: indekste kullanılabilir vektör
    /// yoksa ya da sorgu embed edilemezse sonuç yine <c>lexical</c> döner ve yanıttaki <c>retrievalMode</c> bunu gösterir.
    /// </remarks>
    public async Task<ApiResult<SearchResultDto>> Handle(SearchKnowledgeQuery request, CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim() ?? string.Empty;
        var topK = request.TopK ?? options.Value.TopK;

        var requiredError = rules.CheckQueryRequired<SearchResultDto>(query);
        if (requiredError is not null)
        {
            return requiredError;
        }

        var lengthError = rules.CheckQueryLength<SearchResultDto>(query);
        if (lengthError is not null)
        {
            return lengthError;
        }

        var topKError = rules.CheckTopKInRange<SearchResultDto>(topK);
        if (topKError is not null)
        {
            return topKError;
        }

        var modeError = rules.CheckRetrievalMode<SearchResultDto>(request.Mode);
        if (modeError is not null)
        {
            return modeError;
        }

        var readyError = rules.CheckIndexReady<SearchResultDto>();
        if (readyError is not null)
        {
            return readyError;
        }

        var prepared = await index.PrepareAsync(query, cancellationToken);

        // Sorgu vektörü olmadan indeks yalnızca BM25 ile sıralar; vektörlerin aramaya ne kattığını ölçmek için kullanılır.
        if (string.Equals(request.Mode, "lexical", StringComparison.OrdinalIgnoreCase))
        {
            prepared = prepared with { Vector = null };
        }

        var result = index.Search(prepared, topK);

        var hits = result.Hits
            .Select(hit => new SearchHitDto(
                hit.Chunk.DocumentId,
                hit.Chunk.Title,
                hit.Chunk.Version,
                hit.Chunk.EffectiveDate,
                hit.Chunk.Status.ToApi(),
                hit.Chunk.Category.ToApi(),
                hit.Chunk.SectionPath,
                hit.Chunk.Content,
                hit.FusedScore,
                hit.LexicalScore,
                hit.LexicalCoverage,
                hit.DenseScore))
            .ToList();

        return ApiResult<SearchResultDto>.Ok(new SearchResultDto(query, result.Mode.ToApi(), result.MaxDenseScore, result.MaxLexicalCoverage, hits));
    }
}
