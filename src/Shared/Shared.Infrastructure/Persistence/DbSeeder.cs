using Shared.Application.Abstractions;

namespace Shared.Infrastructure.Persistence;

public sealed class DbSeeder : IDatabaseSeeder
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
