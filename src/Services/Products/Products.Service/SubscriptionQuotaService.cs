using Products.Domain.Repositories;
using Shared.Application.Abstractions;

namespace Products.Service;

public sealed class SubscriptionQuotaService : ISubscriptionQuotaService
{
    private readonly IProductRepository _productRepository;

    public SubscriptionQuotaService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<bool> ExceedsCreateQuotaAsync(
        Guid userId,
        string featureKey,
        string userPlan,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(featureKey, "products", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var limit = ResolveLimit(userPlan);

        if (limit is null)
        {
            return false;
        }

        var existingCount = (await _productRepository.ListAsync(cancellationToken)).Count;

        return existingCount >= limit.Value;
    }

    private static int? ResolveLimit(string userPlan)
    {
        if (string.Equals(userPlan, "free", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (string.Equals(userPlan, "basic", StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        return null;
    }
}
