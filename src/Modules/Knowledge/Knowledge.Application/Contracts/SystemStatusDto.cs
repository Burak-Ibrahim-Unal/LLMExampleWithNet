namespace Knowledge.Application.Contracts;

/// <summary><c>GET /v1/health</c> yanıtı: indeksin, dil modelinin ve embedding yapılandırmasının durumu.</summary>
/// <remarks>
/// Uygulama bilgi tabanı okunamasa ya da model yapılandırılmamış olsa bile açılır; bu uç o durumları "degraded" olarak
/// görünür kılar. Embedding'in olmaması durumu "degraded" yapmaz, çünkü BM25-yalnız mod desteklenen bir çalışma biçimidir.
/// Durum yapılandırmayı yansıtır; uzak sunucuların o an erişilebilir olduğunu ölçmez.
/// </remarks>
/// <param name="Status">İndeks kurulmuş ve bir dil modeli yapılandırılmışsa "ok", aksi hâlde "degraded".</param>
/// <param name="Index">İndeksin durumu.</param>
/// <param name="Llm">Dil modeli yapılandırması.</param>
/// <param name="Embeddings">Embedding yapılandırması; yapılandırılmamışsa arama BM25 ile çalışır.</param>
public sealed record SystemStatusDto(string Status, IndexStatusDto Index, ComponentStatusDto Llm, ComponentStatusDto Embeddings);

/// <summary>Sağlık yanıtındaki indeks durumu.</summary>
/// <param name="Ready">İndeks kurulduysa true; false iken sorular ve aramalar 503 alır.</param>
/// <param name="Documents">İndeksteki doküman sürümü sayısı.</param>
/// <param name="Sections">İndeksteki bölüm (chunk) sayısı.</param>
/// <param name="RetrievalMode">"hybrid" veya "lexical".</param>
/// <param name="BuiltAtUtc">İndeksin son kurulma zamanı (UTC); henüz kurulmadıysa null.</param>
public sealed record IndexStatusDto(bool Ready, int Documents, int Sections, string RetrievalMode, DateTime? BuiltAtUtc);

/// <summary>Bir dış bileşenin (dil modeli veya embedding) yapılandırma durumu.</summary>
/// <param name="Configured">Bileşen için bir uç adresi yapılandırılmışsa true; erişilebilirliği göstermez.</param>
/// <param name="Model">Yapılandırılmış model adı; yapılandırılmamışsa boş.</param>
public sealed record ComponentStatusDto(bool Configured, string Model);
