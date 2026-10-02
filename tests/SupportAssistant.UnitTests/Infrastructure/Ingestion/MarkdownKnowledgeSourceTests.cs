using Knowledge.Application.Exceptions;
using Knowledge.Infrastructure.Ingestion;
using Microsoft.Extensions.Options;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Ingestion;

/// <summary>
/// <see cref="MarkdownKnowledgeSource"/> birim testleri: bilgi tabanı klasöründeki dosyaların gerçek dosya sistemi
/// üzerinden keşfedilip okunmasını doğrular. Her test geçici dizinde kendine ait benzersiz bir klasör kullanır (xUnit her
/// test için sınıfın yeni bir örneğini oluşturur); böylece paralel çalışan testler birbirini etkilemez ve depodaki gerçek
/// <c>knowledge-base/</c> klasörüne dokunulmaz.
/// </summary>
public sealed class MarkdownKnowledgeSourceTests : IDisposable
{
    /// <summary>Bu test örneğine ait, GUID ile benzersizleştirilmiş geçici klasörün mutlak yolu.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"kb-{Guid.NewGuid():N}");

    /// <summary>Her testten önce boş geçici klasörü oluşturur.</summary>
    public MarkdownKnowledgeSourceTests()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <summary>
    /// Verilen kimlikle en küçük geçerli bilgi tabanı dosyasını üretir (LF satır sonları, tek bölüm; kimlik hem <c>id</c>
    /// hem <c>documentKey</c> olur). Bu testlerin konusu ayrıştırma değil dosya keşfi olduğundan içerik bilerek sade
    /// tutulur.
    /// </summary>
    private static string Markdown(string id) =>
        $"---\nid: {id}\ndocumentKey: {id}\ntitle: Belge {id}\nversion: \"1.0\"\neffectiveDate: 2025-01-01\n" +
        "status: active\nsupersedes: \"\"\ncategory: kilavuz\n---\n\n## Bölüm\n\nMetin.\n";

    /// <summary>
    /// Klasördeki her <c>*.md</c> dosyasının ayrıştırıldığını, Markdown olmayan dosyaların (<c>notlar.txt</c>) atlandığını
    /// ve sonuçların, dosyalar ters sırada oluşturulsa bile dosya adına göre (ordinal) sıralandığını doğrular.
    /// </summary>
    /// <remarks>
    /// <c>Directory.EnumerateFiles</c> sırası dosya sistemine göre değişebilir. Sıra sabit olmasaydı chunk sırası ve eşit
    /// skorlu arama sonuçlarının sıralaması makineden makineye değişir, değerlendirme sonuçları tekrarlanamaz olurdu.
    /// Klasöre bırakılan not dosyaları gibi Markdown olmayan dosyalar da ingest'i bozmamalıdır.
    /// </remarks>
    [Fact]
    public async Task LoadAsync_parses_every_markdown_file_in_the_folder_in_name_order()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "b.md"), Markdown("b"), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_folder, "a.md"), Markdown("a"), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_folder, "notlar.txt"), "markdown değil", TestContext.Current.CancellationToken);
        var source = new MarkdownKnowledgeSource(Options.Create(new KnowledgeBaseOptions { Path = _folder }));

        var documents = await source.LoadAsync(TestContext.Current.CancellationToken);

        documents.Select(document => document.SourceId).ShouldBe(["a", "b"]);
    }

    /// <summary>
    /// Yapılandırılan klasör yoksa aranan yolu içeren bir <c>KnowledgeBaseFormatException</c> fırlatıldığını doğrular.
    /// Verilen yol mutlak olduğundan üst dizinlerde arama yapılmaz.
    /// </summary>
    /// <remarks>
    /// Bu istisna ingest'te açıklayıcı bir 422 yanıtına dönüşür. Açık kontrol olmasaydı düşük seviyeli bir
    /// <c>DirectoryNotFoundException</c> yalnızca genel "bilgi tabanı okunamadı" mesajı olarak görünür ve operatör yanlış
    /// yapılandırılmış yolu yanıttan anlayamazdı.
    /// </remarks>
    [Fact]
    public async Task LoadAsync_reports_a_missing_folder()
    {
        var missing = Path.Combine(_folder, "yok");
        var source = new MarkdownKnowledgeSource(Options.Create(new KnowledgeBaseOptions { Path = missing }));

        var exception = await Should.ThrowAsync<KnowledgeBaseFormatException>(() => source.LoadAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain(missing);
    }

    /// <summary>Test bitince geçici klasörü içeriğiyle birlikte siler; geçici dizinde artık dosya birikmez.</summary>
    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
