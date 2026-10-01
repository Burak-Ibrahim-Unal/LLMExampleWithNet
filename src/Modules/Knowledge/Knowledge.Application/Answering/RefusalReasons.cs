namespace Knowledge.Application.Answering;

/// <summary>Why an answer was withheld; one value per gate of the answering pipeline.</summary>
public static class RefusalReasons
{
    /// <summary>Gate 1: retrieval found no section close enough to the question; the model was not called.</summary>
    public const string LowRelevance = "LowRelevance";

    /// <summary>Retrieval found only versions that are not in effect (superseded or future-dated); the model was not called.</summary>
    public const string NoSourceInEffect = "NoSourceInEffect";

    /// <summary>Gate 2: the model judged the provided sources insufficient.</summary>
    public const string ModelInsufficientContext = "ModelInsufficientContext";

    /// <summary>Gate 3: the model answered but cited none of the provided sources.</summary>
    public const string NoValidCitations = "NoValidCitations";
}
