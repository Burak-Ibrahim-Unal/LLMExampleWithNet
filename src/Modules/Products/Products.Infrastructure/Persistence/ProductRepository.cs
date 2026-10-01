using Microsoft.EntityFrameworkCore;
using Products.Domain.Entities;
using Products.Domain.Repositories;
using Shared.Infrastructure.Persistence;

namespace Products.Infrastructure.Persistence;

public sealed class ProductRepository : EfRepository<Product>, IProductRepository
{
    private readonly AppDbContext _dbContext;

    public ProductRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<Product>> ListWithDeletedAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<Product>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Product?> GetByIdWithDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<Product>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(
        string normalizedName,
        Guid? excludingProductId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<Product>()
            .Where(x => x.DeletedAtUtc == null)
            .Where(x => x.Name.ToLower() == normalizedName.ToLower());

        if (excludingProductId is not null)
        {
            query = query.Where(x => x.Id != excludingProductId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }
}
