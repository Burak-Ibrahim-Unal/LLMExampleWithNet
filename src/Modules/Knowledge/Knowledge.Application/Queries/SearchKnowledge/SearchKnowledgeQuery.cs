using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.SearchKnowledge;

/// <summary>
/// Dil modeli kullanmadan yalnızca arama (retrieval) yapma isteği; <c>GET /v1/search?q=&amp;topK=&amp;mode=</c> uç
/// noktasının MediatR karşılığıdır. Asistanın bir soru için hangi bölümleri okuyacağını skorlarıyla birlikte gösterir;
/// hata ayıklamada ve aramanın yanıt üretiminden ayrı ölçüldüğü değerlendirmede kullanılır. Sonuç bir
/// <see cref="SearchResultDto"/> nesnesidir.
/// </summary>
/// <param name="Query">Aranacak metin (genellikle kullanıcının sorusu). Boş olamaz, en fazla 500 karakter olabilir.</param>
/// <param name="TopK">Döndürülecek bölüm sayısı (1–20); null ise yapılandırılmış varsayılan (<c>Retrieval:TopK</c>)
/// kullanılır.</param>
/// <param name="Mode"><c>"lexical"</c> aramayı yalnızca BM25'e zorlar; <c>"hybrid"</c> (veya null) indekste kullanılabilir
/// vektör varsa vektör benzerliğini de kullanır. Büyük/küçük harfe duyarsızdır; başka bir değer 400 ile reddedilir.</param>
public sealed record SearchKnowledgeQuery(string Query, int? TopK, string? Mode = null) : IRequest<ApiResult<SearchResultDto>>;
