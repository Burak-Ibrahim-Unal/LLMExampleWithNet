using Knowledge.Application.Abstractions;
using Knowledge.Infrastructure.Ingestion;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Ingestion;

public sealed class MarkdownSectionChunkerTests
{
    [Fact]
    public void Split_creates_one_chunk_per_heading_with_its_parent_path_and_skips_empty_sections()
    {
        const string body = "# Başlık\n\n## 1. Giriş\n\nMerhaba dünya.\n\n## 2. Seviyeler\n\n### 2.1 Birinci\n\nBirinci metin.\n\n### 2.2 İkinci\n\nİkinci metin.\n";

        var sections = MarkdownSectionChunker.Split(body, "Başlık");

        sections.ShouldBe(
        [
            new SourceSection("1. Giriş", "Merhaba dünya."),
            new SourceSection("2. Seviyeler > 2.1 Birinci", "Birinci metin."),
            new SourceSection("2. Seviyeler > 2.2 İkinci", "İkinci metin.")
        ]);
    }

    [Fact]
    public void Split_uses_the_document_title_as_path_for_text_before_the_first_section()
    {
        var sections = MarkdownSectionChunker.Split("# Başlık\n\nGiriş paragrafı.\n\n## A\n\nMetin.", "Başlık");

        sections.ShouldBe([new SourceSection("Başlık", "Giriş paragrafı."), new SourceSection("A", "Metin.")]);
    }

    [Fact]
    public void Split_breaks_long_sections_at_paragraph_boundaries()
    {
        const string body = "## Uzun\n\nBirinci paragraf burada.\n\nİkinci paragraf burada.\n\nÜçüncü paragraf burada.";

        var sections = MarkdownSectionChunker.Split(body, "Belge", maxChunkChars: 55);

        sections.ShouldBe(
        [
            new SourceSection("Uzun", "Birinci paragraf burada.\n\nİkinci paragraf burada."),
            new SourceSection("Uzun", "Üçüncü paragraf burada.")
        ]);
    }
}
