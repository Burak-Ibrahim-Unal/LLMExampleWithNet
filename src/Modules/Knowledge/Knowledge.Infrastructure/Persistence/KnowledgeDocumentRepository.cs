using Knowledge.Domain.Entities;
using Knowledge.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace Knowledge.Infrastructure.Persistence;

/// <summary>
/// <see cref="IKnowledgeDocumentRepository"/> portunun EF Core uygulaması: genel ekleme/silme/kaydetme işlemlerini
/// <c>EfRepository</c>'den devralır, dokümanı chunk'larıyla birlikte yükleyen iki sorgu ekler.
/// </summary>
/// <remarks>
/// Application katmanı yalnızca domain'deki arayüzü görür; EF Core ve SQLite ayrıntıları burada kalır (mimari testleri
/// bu bağımlılık yönünü zorunlu kılar). DI'da scoped kaydedilir, yani her istek kendi <c>AppDbContext</c>'iyle çalışır.
/// </remarks>
public sealed class KnowledgeDocumentRepository : EfRepository<KnowledgeDocument>, IKnowledgeDocumentRepository
{
    /// <summary>
    /// Temel sınıfın bağlamı private olduğundan, <c>Include</c>'lu özel sorgular için aynı <c>AppDbContext</c> burada da
    /// tutulur.
    /// </summary>
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// İstek kapsamındaki <c>AppDbContext</c>'i hem temel sınıfa hem bu sınıftaki özel sorgulara verir; böylece
    /// eklemeler ve sorgular aynı değişiklik takibini (change tracker) paylaşır.
    /// </summary>
    public KnowledgeDocumentRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Bütün dokümanları chunk'larıyla birlikte, <c>SourceId</c> sırasına göre getirir.
    /// </summary>
    /// <remarks>
    /// Ingestion uzlaştırması (hangi doküman eklendi, değişti, silindi; hangi chunk'ın vektörü eksik), indeksin yeniden
    /// kurulması ve <c>GET /v1/documents</c> (bölüm sayısı) chunk'lara ihtiyaç duyar; <c>Include</c> bunları tek sorguda
    /// getirir ve doküman başına ayrı sorgu (N+1) atılmasını önler. Sorgu bilinçli olarak izlenir (tracked):
    /// ingestion dönen nesneleri değiştirip (<c>Revise</c>, <c>ClearChunks</c>, <c>AddChunk</c>, <c>SetEmbedding</c>)
    /// <c>SaveChangesAsync</c> ile yazar; <c>AsNoTracking</c> bu değişiklikleri görünmez kılardı. Sabit sıralama doküman
    /// listesinin her çağrıda aynı sırada dönmesini sağlar. Bilgi tabanı onlarca chunk büyüklüğünde olduğundan hepsini
    /// belleğe almak sorun değildir.
    /// </remarks>
    public Task<List<KnowledgeDocument>> ListWithChunksAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<KnowledgeDocument>()
            .Include(document => document.Chunks)
            .OrderBy(document => document.SourceId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Tek bir dokümanı front matter'daki kararlı kimliğiyle (<c>SourceId</c>, ör. <c>iade-politikasi-v2</c>) ve
    /// chunk'larıyla birlikte getirir; bulunamazsa <c>null</c> döner.
    /// </summary>
    /// <remarks>
    /// API dokümanları iç Guid yerine bu okunabilir kimlikle adresler (<c>GET /v1/documents/{id}</c>); detay yanıtı
    /// bütün bölümleri gösterdiği için chunk'lar aynı sorguda yüklenir. <c>null</c> sonucu <c>CheckDocumentFound</c> iş
    /// kuralı 404'e çevirir.
    /// </remarks>
    public Task<KnowledgeDocument?> GetBySourceIdWithChunksAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<KnowledgeDocument>()
            .Include(document => document.Chunks)
            .FirstOrDefaultAsync(document => document.SourceId == sourceId, cancellationToken);
    }
}
