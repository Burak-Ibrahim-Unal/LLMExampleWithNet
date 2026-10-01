namespace Knowledge.Application.Exceptions;

public enum AnswerGenerationFailure
{
    /// <summary>The model endpoint could not be reached, timed out or returned a server error.</summary>
    Unavailable,

    /// <summary>The model answered, but not in the required structure, even after a retry.</summary>
    InvalidOutput
}

public sealed class AnswerGenerationException(AnswerGenerationFailure failure, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public AnswerGenerationFailure Failure { get; } = failure;
}
