using Knowledge.Application.Abstractions;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Commands.IngestKnowledgeBase;
using Knowledge.Application.Contracts;
using Knowledge.Application.Options;
using Knowledge.Infrastructure.Persistence;
using Knowledge.Infrastructure.Search;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Application.Common;
using Shared.Infrastructure.Persistence;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Ingestion;

/// <summary>
/// Aynı anda gelen yeniden indeksleme isteklerini sınar; çalışan API'deki gibi her istek kendi <c>DbContext</c>'ini ve
/// dosya tabanlı bir veritabanına kendi bağlantısını kullanır.
/// </summary>
/// <remarks>
/// <para>
/// Açılıştaki ingestion ile <c>POST /v1/documents/reindex</c> (ya da iki reindex isteği) aynı satırlar üzerinde
/// yarışabilir: hepsi aynı kayıtlı durumu okur, aynı chunk'ları silip yeniden ekler ve geç kalan kaydetme bir
/// eşzamanlılık hatasıyla (<c>DbUpdateConcurrencyException</c>) başarısız olur. İncelemede bulunan bu hata, handler'daki
/// statik <c>SemaphoreSlim</c> (<c>IngestionGate</c>) ile giderildi; bu test o düzeltmeyi korur.
/// </para>
/// <para>
/// Düz bir bellek içi SQLite veritabanı yalnızca onu açan bağlantıya aittir; gerçek API'deki gibi bağımsız bağlantılar
/// arasındaki yarışı üretebilmek için geçici bir dosya kullanılır.
/// </para>
/// </remarks>
public sealed class ConcurrentIngestionTests : IAsyncLifetime
{
    /// <summary>
    /// Bu test örneğine özgü geçici veritabanı dosyası; GUID'li ad, paralel ya da ardışık çalıştırmaların aynı dosyayı
    /// paylaşmasını önler.
    /// </summary>
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"ingest-{Guid.NewGuid():N}.db");
    /// <summary>Bilgi tabanı dosyalarının yerine geçen stub; test, iki ingestion arasında içeriği ve hash'i değiştirir.</summary>
    private readonly StubKnowledgeBaseSource _source = new();
    /// <summary>
    /// Embedding sunucusunun yerine geçen fake; <c>Delay</c> ayarı eşzamanlı isteklerin yarış penceresini genişletmek için
    /// kullanılır.
    /// </summary>
    private readonly FakeTextEmbedder _embedder = new();
    /// <summary>Tüm isteklerin paylaştığı gerçek indeks; çalışan uygulamadaki tekil (singleton) indeksin karşılığıdır.</summary>
    private KnowledgeIndex _index = null!;

    /// <summary>İndeksi oluşturur ve dosya veritabanının şemasını <c>EnsureCreatedAsync</c> ile hazırlar.</summary>
    public async ValueTask InitializeAsync()
    {
        _index = new KnowledgeIndex(_embedder, Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Geçici veritabanı dosyasını siler. Önce <c>SqliteConnection.ClearAllPools()</c> çağrılır: Microsoft.Data.Sqlite
    /// bağlantıları havuzda açık tutar ve dosya tanıtıcısı açıkken dosya (özellikle Windows'ta) silinemez.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Bağlantı dizesinden yeni bir <c>AppDbContext</c> oluşturur; her context kendi bağlantısını açar. Bu, gerçek API'de
    /// her isteğin kendi kapsamlı (scoped) <c>DbContext</c>'ini kullanmasının karşılığıdır ve yarışı gerçekçi kılar.
    /// </summary>
    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_databasePath}").Options,
        new EntityConfigurationAssemblyRegistry([Knowledge.Infrastructure.AssemblyReference.Assembly]));

    /// <summary>
    /// Yeni bir context, repository ve handler kurup bir yeniden indeksleme çalıştırır; her çağrı ayrı bir HTTP isteğini
    /// temsil eder. Handler örnekleri farklı olsa da <c>IngestionGate</c> statik olduğundan hepsi aynı kapıyı paylaşır;
    /// test tam olarak bunu sınar.
    /// </summary>
    private async Task<ApiResult<IngestionSummaryDto>> IngestAsync()
    {
        await using var context = CreateContext();
        var handler = new IngestKnowledgeBaseCommandHandler(
            _source, new KnowledgeDocumentRepository(context), _embedder, _index, new KnowledgeBusinessRules(_index),
            NullLogger<IngestKnowledgeBaseCommandHandler>.Instance);

        return await handler.Handle(new IngestKnowledgeBaseCommand(), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Önce ilk ingestion yapılır, ardından dokümanın içeriği değiştirilir (hash-1 → hash-2) ve embedding'e 300 ms gecikme
    /// eklenerek üç yeniden indeksleme aynı anda başlatılır. Üçü de başarılı olmalı; değişikliği yalnızca biri uygulamalı
    /// (toplam <c>Updated = 1</c>), sıradaki diğer ikisi güncel hash'i görüp dokümanı değişmemiş saymalıdır (toplam
    /// <c>Unchanged = 2</c>).
    /// </summary>
    /// <remarks>
    /// Kapı olmasaydı üç istek de eski durumu okuyup aynı chunk'ları değiştirmeye çalışır ve geç kalanlar eşzamanlılık
    /// hatasıyla düşerdi. Sayaçların toplamı, isteklerin yalnızca hatasız bitmediğini, gerçekten sıraya girdiğini de
    /// kanıtlar.
    /// </remarks>
    [Fact]
    public async Task Concurrent_reindex_requests_are_serialised_instead_of_failing()
    {
        _source.Documents = [StubKnowledgeBaseSource.Document("iade", "hash-1", new SourceSection("İade Süresi", "14 gün."), new SourceSection("Kargo", "Müşteri öder."))];
        await IngestAsync();
        _source.Documents = [StubKnowledgeBaseSource.Document("iade", "hash-2", new SourceSection("İade Süresi", "30 gün."), new SourceSection("Kargo", "Ücretsiz."))];

        // Bir istek embedding'leri beklerken diğerleri aynı kayıtlı durumu okuyup kaydetmek için yarışır.
        _embedder.Delay = TimeSpan.FromMilliseconds(300);
        var results = await Task.WhenAll(IngestAsync(), IngestAsync(), IngestAsync());

        results.ShouldAllBe(result => result.Success);
        results.Sum(result => result.Data!.Updated).ShouldBe(1);
        results.Sum(result => result.Data!.Unchanged).ShouldBe(2);
    }
}
