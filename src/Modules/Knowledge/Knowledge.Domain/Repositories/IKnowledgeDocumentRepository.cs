using Knowledge.Domain.Entities;
using Shared.Kernel.Abstractions;

namespace Knowledge.Domain.Repositories;

/// <summary>
/// <see cref="KnowledgeDocument"/> aggregate'i için repository: genel CRUD'a ek olarak dokümanları bölümleriyle
/// (chunk'larıyla) birlikte yükleyen sorgular.
/// </summary>
/// <remarks>
/// Arayüz domain'de, uygulaması (<c>KnowledgeDocumentRepository</c>) Infrastructure'dadır; Application katmanı EF Core'u
/// bilmeden dokümanları okuyup yazabilir. Aggregate her zaman chunk'larıyla birlikte yüklenir, çünkü ingestion
/// karşılaştırması, arama indeksinin kurulması ve doküman uç noktaları bölümlere ihtiyaç duyar.
/// </remarks>
public interface IKnowledgeDocumentRepository : IRepository<KnowledgeDocument>
{
    /// <summary>
    /// Tüm doküman sürümlerini (superseded olanlar dahil) chunk'larıyla birlikte, <c>SourceId</c>'ye göre sıralı getirir.
    /// Dönen nesneler değişiklik takibinde olmalıdır: ingestion onları dosyalarla karşılaştırıp yerinde günceller ve
    /// kaydeder. Doküman listesi uç noktası da bölüm sayılarını buradan hesaplar.
    /// </summary>
    Task<List<KnowledgeDocument>> ListWithChunksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Front matter kimliğiyle (ör. "iade-politikasi-v2") tek bir dokümanı chunk'larıyla birlikte getirir; yoksa
    /// <c>null</c>. <c>GET /v1/documents/{id}</c> dokümanları teknik Guid ile değil bu okunabilir kimlikle sunar.
    /// </summary>
    Task<KnowledgeDocument?> GetBySourceIdWithChunksAsync(string sourceId, CancellationToken cancellationToken = default);
}
