using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Abstractions;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// <see cref="IRepository{T}"/> sözleşmesinin EF Core uygulaması; modül repository'lerinin (ör.
/// <c>KnowledgeDocumentRepository</c>, <c>QuestionLogRepository</c>) temel sınıfıdır.
/// </summary>
/// <remarks>
/// Ortak CRUD kodunu tek yerde toplar; modül repository'leri yalnızca kendilerine özgü sorguları (ör. chunk'larıyla
/// birlikte yükleme) ekler. Bir DI scope'u (istek) içindeki tüm repository'ler aynı scoped <see cref="AppDbContext"/>
/// örneğini paylaşır; bu yüzden <see cref="SaveChangesAsync"/> o scope'taki tüm bekleyen değişiklikleri tek seferde
/// yazar (unit of work).
/// </remarks>
/// <typeparam name="T">Kalıcı varlık tipi.</typeparam>
public class EfRepository<T> : IRepository<T> where T : EntityBase
{
    private readonly AppDbContext _dbContext;
    private readonly DbSet<T> _set;

    /// <summary>Paylaşılan context'i alır ve <typeparamref name="T"/> için <c>DbSet</c>'i bir kez çözüp saklar.</summary>
    public EfRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _set = dbContext.Set<T>();
    }

    /// <summary>
    /// Varlığı Guid anahtarıyla, değişiklik takibi açık olarak getirir; böylece dönen nesnede yapılan değişiklikler
    /// <see cref="SaveChangesAsync"/> ile kaydedilebilir. Bulunamazsa <c>null</c>. Knowledge modülünde şu an kullanılmıyor.
    /// </summary>
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _set.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>
    /// Tüm varlıkları <c>AsNoTracking</c> ile listeler: liste salt okuma içindir, takip maliyeti gereksizdir; dönen
    /// nesnelerdeki değişiklikler bu yüzden kaydedilmez. Knowledge modülünde şu an kullanılmıyor.
    /// </summary>
    public async Task<List<T>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _set.AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <summary>Varlığı <c>Added</c> durumuna alır; INSERT, <see cref="SaveChangesAsync"/> çağrısında yapılır.</summary>
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _set.AddAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Varlığı (ve ondan erişilebilen ilişkili nesneleri) <c>Modified</c> olarak işaretler; takip dışındaki bir nesneyi
    /// kaydetmek içindir. Knowledge modülünde şu an kullanılmıyor.
    /// </summary>
    public void Update(T entity)
    {
        _set.Update(entity);
    }

    /// <summary>
    /// Varlığı <c>Deleted</c> durumuna alır; DELETE, <see cref="SaveChangesAsync"/> çağrısında yapılır. Bir dokümanın
    /// chunk'ları, ilişki yapılandırmasındaki cascade sayesinde onunla birlikte silinir.
    /// </summary>
    public void Remove(T entity)
    {
        _set.Remove(entity);
    }

    /// <summary>
    /// Paylaşılan context'teki bekleyen tüm değişiklikleri kaydeder: yalnızca <typeparamref name="T"/> değil, aynı
    /// scope'taki diğer repository'lerin değişiklikleri de yazılır. <see cref="AppDbContext"/> bu sırada güncelleme
    /// damgasını basar.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
