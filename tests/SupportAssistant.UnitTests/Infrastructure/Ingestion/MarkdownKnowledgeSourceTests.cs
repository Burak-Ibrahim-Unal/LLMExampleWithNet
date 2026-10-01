using Knowledge.Application.Exceptions;
using Knowledge.Infrastructure.Ingestion;
using Microsoft.Extensions.Options;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Ingestion;

public sealed class MarkdownKnowledgeSourceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"kb-{Guid.NewGuid():N}");

    public MarkdownKnowledgeSourceTests()
    {
        Directory.CreateDirectory(_folder);
    }

    private static string Markdown(string id) =>
        $"---\nid: {id}\ndocumentKey: {id}\ntitle: Belge {id}\nversion: \"1.0\"\neffectiveDate: 2025-01-01\n" +
        "status: active\nsupersedes: \"\"\ncategory: kilavuz\n---\n\n## Bölüm\n\nMetin.\n";

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

    [Fact]
    public async Task LoadAsync_reports_a_missing_folder()
    {
        var missing = Path.Combine(_folder, "yok");
        var source = new MarkdownKnowledgeSource(Options.Create(new KnowledgeBaseOptions { Path = missing }));

        var exception = await Should.ThrowAsync<KnowledgeBaseFormatException>(() => source.LoadAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain(missing);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
