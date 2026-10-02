namespace Knowledge.Application.Contracts;

/// <summary>
/// <c>GET /v1/documents</c> listesindeki bir doküman sürümünün özeti; liste hafif kalsın diye içerik yerine bölüm sayısı döner.
/// </summary>
/// <param name="Id">Front matter'daki doküman kimliği, ör. "iade-politikasi-v2".</param>
/// <param name="DocumentKey">Aynı prosedürün tüm sürümlerince paylaşılır; sürümleri bir aile olarak gruplamaya yarar.</param>
/// <param name="Title">Doküman başlığı.</param>
/// <param name="Version">Sürüm numarası (front matter'daki değer).</param>
/// <param name="EffectiveDate">Yürürlük tarihi; sürüm seçiminde belirleyicidir.</param>
/// <param name="Status">"active" veya "superseded".</param>
/// <param name="Category">"politika", "prosedur", "kilavuz" veya "sss".</param>
/// <param name="Supersedes">Bu sürümün yerini aldığı sürümün kimliği; yoksa null.</param>
/// <param name="SectionCount">Dokümanın indekslenen bölüm (chunk) sayısı.</param>
public sealed record DocumentSummaryDto(
    string Id,
    string DocumentKey,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    string Status,
    string Category,
    string? Supersedes,
    int SectionCount);

/// <summary>
/// <c>GET /v1/documents/{id}</c> yanıtı: doküman meta verisi ve sıralı bölümleri. Bir atıfta adı geçen bölümün tam metnine
/// ulaşmayı sağlar.
/// </summary>
/// <param name="Id">Front matter'daki doküman kimliği, ör. "iade-politikasi-v2".</param>
/// <param name="DocumentKey">Doküman ailesi anahtarı; aynı prosedürün sürümlerince paylaşılır.</param>
/// <param name="Title">Doküman başlığı.</param>
/// <param name="Version">Sürüm numarası (front matter'daki değer).</param>
/// <param name="EffectiveDate">Yürürlük tarihi; sürüm seçiminde belirleyicidir.</param>
/// <param name="Status">"active" veya "superseded".</param>
/// <param name="Category">"politika", "prosedur", "kilavuz" veya "sss".</param>
/// <param name="Supersedes">Bu sürümün yerini aldığı sürümün kimliği; yoksa null. Yalnızca bilgi amaçlıdır.</param>
/// <param name="Sections">Dokümandaki sıralarına göre bölümler.</param>
public sealed record DocumentDetailDto(
    string Id,
    string DocumentKey,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    string Status,
    string Category,
    string? Supersedes,
    IReadOnlyList<DocumentSectionDto> Sections);

/// <summary>Dokümanın tek bir bölümü.</summary>
/// <param name="Order">Bölümün doküman içindeki sırası (0'dan başlar).</param>
/// <param name="Section">Bölüm yolu (başlık hiyerarşisi); atıflarda görünen değerle aynıdır.</param>
/// <param name="Content">Bölüm metni.</param>
public sealed record DocumentSectionDto(int Order, string Section, string Content);
