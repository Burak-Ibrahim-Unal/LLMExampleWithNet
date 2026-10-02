using Microsoft.EntityFrameworkCore;
using Shared.Application.Abstractions;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// Açılışta veritabanı şemasını hazırlayan <see cref="IDatabaseMigrator"/> uygulaması: EF Core migration'ları varsa
/// onları uygular, yoksa şemayı doğrudan modelden (<c>EnsureCreated</c>) oluşturur.
/// </summary>
/// <remarks>
/// Projede migration yoktur: veritabanı <c>knowledge-base/</c> dosyalarından türetilen veridir ve her açılışta ingestion
/// ile yeniden uzlaştırılır, bu yüzden şemayı modelden oluşturmak yeterlidir. Bunun bilinen sınırı, <c>EnsureCreated</c>'in
/// mevcut bir şemaya eksik tabloları eklememesidir; şema değişirse veritabanı dosyası elle silinir ve açılışta yeniden
/// oluşturulur (README, bilinen sınırlar). İleride migration eklenirse aynı sınıf kendiliğinden <c>Migrate</c> yoluna geçer.
/// </remarks>
public sealed class DbMigrator : IDatabaseMigrator
{
    private readonly AppDbContext _dbContext;

    /// <summary>Scoped <see cref="AppDbContext"/> ile oluşturulur; açılışta oluşturulan DI scope'u içinde çözülür.</summary>
    public DbMigrator(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Şemayı hazırlar; mevcut şema eksiksizse hiçbir şey yapmaz, böylece her açılışta güvenle çalıştırılabilir.
    /// </summary>
    /// <remarks>
    /// Migration yokken modeldeki tablo adları SQLite'taki mevcut tablolarla karşılaştırılır; eksik yoksa çıkılır. Eksik
    /// varsa ve dosyada yalnızca altyapı tabloları (<c>__EFMigrationsHistory</c>, <c>sqlite_*</c>) bulunuyorsa veritabanı
    /// önce silinir: EF Core'un <c>EnsureCreated</c> metodu herhangi bir tablo gördüğü veritabanında hiçbir şey yapmadığı
    /// için, aksi hâlde şema hiç oluşturulmazdı. Ardından <c>EnsureCreated</c> modeldeki tabloları oluşturur.
    /// </remarks>
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

        // Dosyada yalnızca EF/SQLite altyapı tabloları varsa EnsureCreated "tablo var" deyip hiçbir şey yapmazdı;
        // veritabanı bu yüzden önce silinir ve şema temiz biçimde oluşturulur.
        var onlyInfraTables = existingTables.All(x =>
            x.Equals("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase)
            || x.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase));

        if (onlyInfraTables)
        {
            await _dbContext.Database.EnsureDeletedAsync(cancellationToken);
        }

        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>
    /// SQLite sistem kataloğundan (<c>sqlite_master</c>) mevcut tablo adlarını büyük/küçük harf duyarsız bir kümede
    /// döndürür. Sorgu SQLite'a özgüdür; başka bir veritabanı sağlayıcısına geçilirse bu metodun da değişmesi gerekir.
    /// </summary>
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

