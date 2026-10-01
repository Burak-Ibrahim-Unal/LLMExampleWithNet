namespace Knowledge.Application.Abstractions;

/// <summary>Language model step: answers a question from the given sources only and cites them by label.</summary>
public interface IGroundedAnswerGenerator
{
    bool IsConfigured { get; }

    string ModelName { get; }

    /// <exception cref="Exceptions.AnswerGenerationException">The model is unreachable or keeps returning invalid output.</exception>
    Task<GeneratedAnswer> GenerateAsync(string question, IReadOnlyList<ContextChunk> context, CancellationToken cancellationToken = default);
}

/// <param name="Label">Short identifier shown to the model ("C1", "C2", ...); the model cites sources by it.</param>
public sealed record ContextChunk(string Label, IndexedChunk Chunk);

public sealed record GeneratedAnswer(
    bool Answerable,
    string Answer,
    IReadOnlyList<GeneratedCitation> Citations,
    string MissingInformation,
    IReadOnlyList<GeneratedConflict> Conflicts,
    string Model,
    long? InputTokens,
    long? OutputTokens);

public sealed record GeneratedCitation(string ChunkLabel, string Quote);

public sealed record GeneratedConflict(string Topic, string ChosenChunkLabel, IReadOnlyList<string> RejectedChunkLabels, string Reason);
