using Microsoft.EntityFrameworkCore;
using Shared.Application.Abstractions;

namespace Shared.Infrastructure.Persistence;

public sealed class DbMigrator : IDatabaseMigrator
{
    private readonly AppDbContext _dbContext;

    public DbMigrator(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var hasMigrations = _dbContext.Database.GetMigrations().Any();

        if (hasMigrations)
        {
            await _dbContext.Database.MigrateAsync(cancellationToken);
            return;
        }

        var expectedTables = _dbContext.Model
            .GetEntityTypes()
            .Select(x => x.GetTableName())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingTables = await GetExistingTableNamesAsync(cancellationToken);
        var missingTables = expectedTables.Except(existingTables, StringComparer.OrdinalIgnoreCase).ToArray();

        if (missingTables.Length == 0)
        {
            return;
        }

        var onlyInfraTables = existingTables.All(x =>
            x.Equals("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase)
            || x.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase));

        if (onlyInfraTables)
        {
            await _dbContext.Database.EnsureDeletedAsync(cancellationToken);
        }

        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    private async Task<HashSet<string>> GetExistingTableNamesAsync(CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}

