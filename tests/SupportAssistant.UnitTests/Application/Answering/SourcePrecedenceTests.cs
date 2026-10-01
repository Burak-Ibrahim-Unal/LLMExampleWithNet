using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Domain.Entities;
using Shouldly;

namespace SupportAssistant.UnitTests.Application.Answering;

public sealed class SourcePrecedenceTests
{
    private static IndexedChunk Chunk(DocumentCategory category, int year) =>
        new(Guid.NewGuid(), $"{category}-{year}", "key", "Başlık", "1.0", new DateOnly(year, 1, 1),
            DocumentStatus.Active, category, "Bölüm", "metin");

    [Theory]
    [InlineData(DocumentCategory.Policy, 2025, DocumentCategory.Faq, 2024, true)]
    [InlineData(DocumentCategory.Faq, 2024, DocumentCategory.Policy, 2025, false)]
    [InlineData(DocumentCategory.Policy, 2023, DocumentCategory.Faq, 2025, true)]  // authority beats recency
    [InlineData(DocumentCategory.Guide, 2025, DocumentCategory.Faq, 2025, true)]
    [InlineData(DocumentCategory.Procedure, 2025, DocumentCategory.Policy, 2024, true)] // same rank: newer wins
    [InlineData(DocumentCategory.Procedure, 2024, DocumentCategory.Policy, 2025, false)]
    public void Policies_and_procedures_outrank_guides_which_outrank_faqs_and_newer_wins_within_a_rank(
        DocumentCategory candidateCategory, int candidateYear, DocumentCategory otherCategory, int otherYear, bool expected)
    {
        SourcePrecedence.Outranks(Chunk(candidateCategory, candidateYear), Chunk(otherCategory, otherYear)).ShouldBe(expected);
    }
}
