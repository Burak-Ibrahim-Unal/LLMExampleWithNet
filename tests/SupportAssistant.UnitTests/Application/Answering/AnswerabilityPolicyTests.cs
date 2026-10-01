using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.Options;
using Microsoft.Extensions.Options;
using Shouldly;

namespace SupportAssistant.UnitTests.Application.Answering;

public sealed class AnswerabilityPolicyTests
{
    private static readonly AnswerabilityPolicy Policy = new(Options.Create(new RetrievalOptions { MinDenseScore = 0.55, MinLexicalCoverage = 0.5 }));

    [Theory]
    [InlineData(RetrievalMode.Hybrid, 0.70, 0.10, true)]  // paraphrase: only vectors find it
    [InlineData(RetrievalMode.Hybrid, 0.38, 1.00, true)]  // typed without Turkish characters: only words find it
    [InlineData(RetrievalMode.Hybrid, 0.43, 0.10, false)] // unrelated question
    [InlineData(RetrievalMode.Lexical, 0.00, 0.49, false)]
    [InlineData(RetrievalMode.Lexical, 0.00, 0.50, true)]
    public void Evidence_is_enough_when_either_the_vector_or_the_word_signal_clears_its_threshold(
        RetrievalMode mode, double maxDenseScore, double maxLexicalCoverage, bool expected)
    {
        var result = new SearchResult(mode, [], maxDenseScore, maxLexicalCoverage);

        Policy.HasEnoughEvidence(result).ShouldBe(expected);
    }
}
