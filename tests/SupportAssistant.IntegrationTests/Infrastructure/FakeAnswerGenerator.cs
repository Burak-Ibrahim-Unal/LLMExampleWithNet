using Knowledge.Application.Abstractions;

namespace SupportAssistant.IntegrationTests.Infrastructure;

/// <summary>Replaces the language model in API tests: answers by quoting the first source it receives.</summary>
public sealed class FakeAnswerGenerator : IGroundedAnswerGenerator
{
    public bool IsConfigured => true;

    public string ModelName => "fake-llm";

    public Task<GeneratedAnswer> GenerateAsync(string question, IReadOnlyList<ContextChunk> context, CancellationToken cancellationToken = default)
    {
        var first = context[0];
        return Task.FromResult(new GeneratedAnswer(
            Answerable: true,
            Answer: first.Chunk.Content,
            Citations: [new GeneratedCitation(first.Label, first.Chunk.Content)],
            MissingInformation: string.Empty,
            Conflicts: [],
            Model: ModelName,
            InputTokens: null,
            OutputTokens: null));
    }
}
