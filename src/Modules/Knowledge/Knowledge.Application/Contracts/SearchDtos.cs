namespace Knowledge.Application.Contracts;

/// <summary>
/// <c>GET /v1/search</c> yanıtı: dil modeli olmadan, bir soru için hangi bölümlerin hangi skorlarla bulunduğu.
/// </summary>
/// <remarks>
/// Bu uç aramayı yanıt üretiminden ayrı ölçmek için vardır: değerlendirme aracı beklenen kaynağın bulunup bulunmadığını
/// <c>mode=lexical</c> ve <c>mode=hybrid</c> için ayrı ayrı kontrol eder. Kapı 1'in kullandığı en iyi skorlar da döner;
/// bir sorunun neden reddedildiği buradan görülebilir.
/// </remarks>
/// <param name="Query">Kırpılmış arama ifadesi.</param>
/// <param name="RetrievalMode">"hybrid" (BM25 + vektörler) veya "lexical" (yalnızca BM25).</param>
/// <param name="MaxDenseScore">Tüm bölümler arasındaki en iyi kosinüs benzerliği (lexical modda 0).</param>
/// <param name="MaxLexicalCoverage">Sorgu terimlerinin tek bir bölümde bulunan en yüksek (idf ağırlıklı) payı.</param>
/// <param name="Hits">Füzyon skoruna göre sıralı sonuçlar.</param>
public sealed record SearchResultDto(
    string Query,
    string RetrievalMode,
    double MaxDenseScore,
    double MaxLexicalCoverage,
    IReadOnlyList<SearchHitDto> Hits);

/// <summary>Tek bir arama sonucu: bölüm, meta verisi ve skorları ayrı ayrı.</summary>
/// <param name="DocumentId">Bölümün ait olduğu doküman sürümünün kimliği.</param>
/// <param name="Title">Doküman başlığı.</param>
/// <param name="Version">Doküman sürümü.</param>
/// <param name="EffectiveDate">Doküman sürümünün yürürlük tarihi.</param>
/// <param name="Status">"active" veya "superseded"; arama sürüm çözümünden önceki sonucu gösterdiği için eski sürümler de görünebilir.</param>
/// <param name="Category">"politika", "prosedur", "kilavuz" veya "sss".</param>
/// <param name="Section">Bölüm yolu (başlık hiyerarşisi).</param>
/// <param name="Content">Bölüm metni; skorların neden böyle çıktığını incelemek için tam olarak döner.</param>
/// <param name="Score">Sonuçları sıralayan Reciprocal Rank Fusion skoru.</param>
/// <param name="LexicalScore">Ham BM25 skoru; yalnızca aynı sorgunun sonuçları arasında karşılaştırılabilir.</param>
/// <param name="LexicalCoverage">Sorgu terimlerinin bu bölümde bulunan idf ağırlıklı payı (0..1).</param>
/// <param name="DenseScore">Kosinüs benzerliği; lexical modda null.</param>
public sealed record SearchHitDto(
    string DocumentId,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    string Status,
    string Category,
    string Section,
    string Content,
    double Score,
    double LexicalScore,
    double LexicalCoverage,
    double? DenseScore);
