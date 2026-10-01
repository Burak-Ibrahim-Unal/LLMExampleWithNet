using Products.Domain.Entities;
using Shared.Kernel.Abstractions;

namespace Products.Domain.Repositories;

public interface IProductRepository : IRepository<Product>
{
    Task<List<Product>> ListWithDeletedAsync(CancellationToken cancellationToken = default);

    Task<Product?> GetByIdWithDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        string normalizedName,
        Guid? excludingProductId = null,
        CancellationToken cancellationToken = default);
}
