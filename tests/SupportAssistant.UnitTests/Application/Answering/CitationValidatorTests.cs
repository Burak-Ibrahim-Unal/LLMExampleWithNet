using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Domain.Entities;
using Shouldly;

namespace SupportAssistant.UnitTests.Application.Answering;

public sealed class CitationValidatorTests
{
    private static ContextChunk Source(string label, string content) =>
        new(label, new IndexedChunk(Guid.NewGuid(), $"doc-{label}", "iade", "İade", "2.0", new DateOnly(2025, 6, 1),
            DocumentStatus.Active, DocumentCategory.Policy, "Bölüm", content));

    private static readonly IReadOnlyList<ContextChunk> Context =
    [
        Source("C1", "Müşteriler, ürünü teslim aldıkları tarihten itibaren 30 gün içinde iade talebinde bulunabilir."),
        Source("C2", "İade kargosu ücretsizdir.")
    ];

    [Fact]
    public void Citations_to_sources_that_were_not_provided_are_dropped()
    {
        var validated = CitationValidator.Validate([new("C9", "uydurma"), new("C1", "30 gün içinde iade talebinde bulunabilir")], Context);

        validated.ShouldHaveSingleItem().Source.Label.ShouldBe("C1");
    }

    [Theory]
    [InlineData("C1", "30 gün içinde iade talebinde bulunabilir")]
    [InlineData("c1", "30 GUN ICINDE IADE TALEBINDE")]
    [InlineData("[C1]", "teslim aldıkları tarihten itibaren 30 gün")]
    public void A_quote_found_in_the_source_is_verified_regardless_of_case_or_turkish_characters(string label, string quote)
    {
        CitationValidator.Validate([new(label, quote)], Context).ShouldHaveSingleItem().QuoteVerified.ShouldBeTrue();
    }

    [Fact]
    public void A_paraphrased_quote_keeps_its_source_but_is_marked_unverified()
    {
        var citation = CitationValidator.Validate([new("C2", "iade kargosu bedava")], Context).ShouldHaveSingleItem();

        citation.Source.Label.ShouldBe("C2");
        citation.QuoteVerified.ShouldBeFalse();
    }

    [Fact]
    public void Repeated_citations_are_collapsed()
    {
        CitationValidator.Validate([new("C2", "İade kargosu ücretsizdir."), new("C2", "İade kargosu ücretsizdir.")], Context)
            .ShouldHaveSingleItem();
    }
}
