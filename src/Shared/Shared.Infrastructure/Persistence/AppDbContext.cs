using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Abstractions;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// Tüm modüllerin paylaştığı tek EF Core <c>DbContext</c>'i (SQLite). Varlık tiplerini doğrudan bilmez; modelini
/// modüllerin <c>IEntityTypeConfiguration</c> sınıflarından kurar ve kayıt sırasında güncelleme damgasını basar.
/// </summary>
/// <remarks>
/// Modüler monolitte her modül için ayrı bir DbContext yerine tek context kullanmak, tek bir SQLite dosyası ve tek bir
/// şema oluşturma adımı demektir. Modüllerin tablolarını <see cref="EntityConfigurationAssemblyRegistry"/> aracılığıyla
/// almak, <c>Shared.Infrastructure</c>'ın modül projelerini referans almasını gerektirmez; bağımlılık yönü korunur.
/// </remarks>
public sealed class AppDbContext : DbContext
{
    private readonly EntityConfigurationAssemblyRegistry _configurationRegistry;

    /// <summary>
    /// Seçenekler ve yapılandırma derlemeleri kaydıyla oluşturulur. Kayıt bir constructor parametresi olduğu için birim
    /// testleri de context'i aynı Knowledge yapılandırmalarıyla, DI kurmadan elle oluşturabilir.
    /// </summary>
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        EntityConfigurationAssemblyRegistry configurationRegistry)
        : base(options)
    {
        _configurationRegistry = configurationRegistry;
    }

    /// <summary>
    /// Kayıttaki her derlemenin <c>IEntityTypeConfiguration</c> sınıflarını modele uygular. Tablo adları, anahtarlar,
    /// dönüştürücüler (ör. embedding <c>float[]</c> → BLOB) ve indeksler böylece ilgili modülün Infrastructure projesinde,
    /// kendi varlıklarının yanında tanımlanır.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var assembly in _configurationRegistry.Assemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }

    /// <summary>
    /// Kaydetmeden önce değişen varlıkların güncelleme zamanını damgalar, ardından EF Core'un kaydetme işlemini çalıştırır.
    /// Uygulamadaki kayıtlar repository'ler üzerinden bu asenkron yoldan geçer.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Senkron kaydetmede de aynı damgalamayı uygular; hangi kaydetme yolu kullanılırsa kullanılsın
    /// <see cref="EntityBase.UpdatedAtUtc"/> tutarlı kalır.
    /// </summary>
    public override int SaveChanges()
    {
        StampEntities();
        return base.SaveChanges();
    }

    /// <summary>
    /// Değişiklik takibindeki <see cref="EntityBase"/> varlıklarından <c>Modified</c> durumda olanların
    /// <see cref="EntityBase.UpdatedAtUtc"/> değerini şimdiye ayarlar. Yeni eklenenler (<c>Added</c>) damgalanmaz; onların
    /// zamanı <see cref="EntityBase.CreatedAtUtc"/>'dir.
    /// </summary>
    /// <remarks>
    /// <c>ChangeTracker.Entries</c> çağrısı değişiklik algılamayı tetiklediği için yalnızca bir alanı gerçekten değişmiş
    /// varlıklar (ör. <c>Revise</c> edilen doküman, embedding'i yenilenen chunk) <c>Modified</c> görünür.
    /// </remarks>
    private void StampEntities()
    {
        var trackedEntities = ChangeTracker.Entries<EntityBase>();

        foreach (var entry in trackedEntities)
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.MarkUpdated();
            }
        }
    }
}
