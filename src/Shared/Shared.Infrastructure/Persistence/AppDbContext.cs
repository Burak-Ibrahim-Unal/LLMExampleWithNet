using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Abstractions;

namespace Shared.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    private readonly EntityConfigurationAssemblyRegistry _configurationRegistry;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        EntityConfigurationAssemblyRegistry configurationRegistry)
        : base(options)
    {
        _configurationRegistry = configurationRegistry;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var assembly in _configurationRegistry.Assemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampEntities();
        return base.SaveChanges();
    }

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
