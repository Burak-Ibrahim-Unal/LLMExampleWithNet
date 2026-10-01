namespace Shared.Application.Abstractions;

public interface ISubscriptionQuotaService
{
    Task<bool> ExceedsCreateQuotaAsync(
        Guid userId,
        string featureKey,
        string userPlan,
        CancellationToken cancellationToken = default);
}
