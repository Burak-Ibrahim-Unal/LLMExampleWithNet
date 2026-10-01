using Knowledge.Application.Abstractions;

namespace SupportAssistant.UnitTests.TestDoubles;

/// <summary>Stands in for the language model. By default it answers by quoting the first source it receives.</summary>
internal sealed class FakeAnswerGenerator : IGroundedAnswerGenerator
{
    public bool IsConfigured => true;

    public string ModelName => "fake-llm";

    public int Calls { get; private set; }

    public IReadOnlyList<ContextChunk> LastContext { get; private set; } = [];

    public Func<string, IReadOnlyList<ContextChunk>, GeneratedAnswer> Respond { get; set; } = QuoteFirstSource;

    public Exception? Failure { get; set; }

    public Task<GeneratedAnswer> GenerateAsync(string question, IReadOnlyList<ContextChunk> context, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastContext = context;

        return Failure is not null ? Task.FromException<GeneratedAnswer>(Failure) : Task.FromResult(Respond(question, context));
    }

    public static GeneratedAnswer QuoteFirstSource(string question, IReadOnlyList<ContextChunk> context) =>
        Answer(context[0].Chunk.Content, new GeneratedCitation(context[0].Label, context[0].Chunk.Content));

    public static GeneratedAnswer Answer(string text, params GeneratedCitation[] citations) =>
        new(true, text, citations, string.Empty, [], "fake-llm", 10, 5);

    public static GeneratedAnswer NotAnswerable(string missingInformation) =>
        new(false, string.Empty, [], missingInformation, [], "fake-llm", 10, 5);
}
