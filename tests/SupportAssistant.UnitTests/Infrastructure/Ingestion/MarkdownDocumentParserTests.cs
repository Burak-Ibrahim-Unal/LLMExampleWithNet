using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Ingestion;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Ingestion;

public sealed class MarkdownDocumentParserTests
{
    private const string FileName = "iade-v2.md";

    // Git on Windows checks the knowledge base out with CRLF line endings.
    private const string Valid =
        "---\r\nid: iade-v2\r\ndocumentKey: iade\r\ntitle: İade Politikası\r\nversion: \"2.0\"\r\neffectiveDate: 2025-06-01\r\n" +
        "status: active\r\nsupersedes: iade-v1\r\ncategory: politika\r\n---\r\n\r\n# İade Politikası\r\n\r\n" +
        "## 2. İade Süresi\r\n\r\n30 gün içinde iade edebilirsiniz.\r\n";

    [Fact]
    public void Parse_reads_front_matter_and_sections_from_crlf_text()
    {
        var document = MarkdownDocumentParser.Parse(FileName, Valid);

        document.SourceId.ShouldBe("iade-v2");
        document.DocumentKey.ShouldBe("iade");
        document.Title.ShouldBe("İade Politikası");
        document.Version.ShouldBe("2.0");
        document.EffectiveDate.ShouldBe(new DateOnly(2025, 6, 1));
        document.Status.ShouldBe(DocumentStatus.Active);
        document.Category.ShouldBe(DocumentCategory.Policy);
        document.Supersedes.ShouldBe("iade-v1");
        document.Sections.ShouldBe([new SourceSection("2. İade Süresi", "30 gün içinde iade edebilirsiniz.")]);
    }

    [Fact]
    public void Parse_treats_an_empty_supersedes_value_as_none()
    {
        var document = MarkdownDocumentParser.Parse(FileName, Valid.Replace("supersedes: iade-v1", "supersedes: \"\""));

        document.Supersedes.ShouldBeNull();
    }

    [Fact]
    public void Parse_rejects_a_document_without_a_required_field()
    {
        var exception = Should.Throw<KnowledgeBaseFormatException>(
            () => MarkdownDocumentParser.Parse(FileName, Valid.Replace("documentKey: iade\r\n", string.Empty)));

        exception.Message.ShouldContain(FileName);
        exception.Message.ShouldContain("documentKey");
    }

    [Theory]
    [InlineData("status: active", "status: draft")]
    [InlineData("category: politika", "category: blog")]
    [InlineData("effectiveDate: 2025-06-01", "effectiveDate: 01.06.2025")]
    public void Parse_rejects_unknown_status_category_or_date_format(string original, string replacement)
    {
        Should.Throw<KnowledgeBaseFormatException>(() => MarkdownDocumentParser.Parse(FileName, Valid.Replace(original, replacement)));
    }

    [Fact]
    public void Parse_rejects_empty_front_matter_instead_of_crashing()
    {
        var exception = Should.Throw<KnowledgeBaseFormatException>(() => MarkdownDocumentParser.Parse("bos.md", "---\n---\n\n## A\n\nMetin."));

        exception.Message.ShouldContain("bos.md");
    }

    [Fact]
    public void Parse_rejects_text_without_front_matter()
    {
        Should.Throw<KnowledgeBaseFormatException>(() => MarkdownDocumentParser.Parse(FileName, "# Başlık\n\n## A\n\nMetin."));
    }

    [Fact]
    public void Content_hash_ignores_line_endings_but_changes_with_the_text()
    {
        var original = MarkdownDocumentParser.Parse(FileName, Valid).ContentHash;

        MarkdownDocumentParser.Parse(FileName, Valid.Replace("\r\n", "\n")).ContentHash.ShouldBe(original);
        MarkdownDocumentParser.Parse(FileName, Valid.Replace("30 gün", "45 gün")).ContentHash.ShouldNotBe(original);
    }
}
