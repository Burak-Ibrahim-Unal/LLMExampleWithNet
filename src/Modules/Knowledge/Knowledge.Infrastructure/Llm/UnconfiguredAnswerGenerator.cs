using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;

namespace Knowledge.Infrastructure.Llm;

/// <summary>Used when Llm:BaseUrl is empty: search still works, questions report the model as unavailable.</summary>
public sealed class UnconfiguredAnswerGenerator : IGroundedAnswerGenerator
{
    public bool IsConfigured => false;

    public string ModelName => string.Empty;

    public Task<GeneratedAnswer> GenerateAsync(string question, IReadOnlyList<ContextChunk> context, CancellationToken cancellationToken = default)
        => throw new AnswerGenerationException(AnswerGenerationFailure.Unavailable, "No language model is configured (Llm:BaseUrl is empty).");
}
