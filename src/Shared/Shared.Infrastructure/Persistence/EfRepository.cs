using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Abstractions;

namespace Shared.Infrastructure.Persistence;

public class EfRepository<T> : IRepository<T> where T : EntityBase
{
    private readonly AppDbContext _dbContext;
    private readonly DbSet<T> _set;

    public EfRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _set = dbContext.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _set.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<T>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _set.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _set.AddAsync(entity, cancellationToken);
    }

    public void Update(T entity)
    {
        _set.Update(entity);
    }

    public void Remove(T entity)
    {
        _set.Remove(entity);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
