using Knowledge.Application.Abstractions;
using Knowledge.Application.Options;
using Microsoft.Extensions.Options;

namespace Knowledge.Application.Answering;

/// <summary>
/// Gate 1, before the language model: is anything in the knowledge base close enough to the question?
/// Either signal suffices. Vector similarity catches paraphrases ("paramı ne zaman alırım" → "para iadesi"),
/// word coverage catches what the embedding model handles poorly, e.g. Turkish typed without Turkish characters.
/// </summary>
public sealed class AnswerabilityPolicy(IOptions<RetrievalOptions> options)
{
    public bool HasEnoughEvidence(SearchResult result)
    {
        var settings = options.Value;
        var vectorEvidence = result.Mode == RetrievalMode.Hybrid && result.MaxDenseScore >= settings.MinDenseScore;
        var wordEvidence = result.MaxLexicalCoverage >= settings.MinLexicalCoverage;

        return vectorEvidence || wordEvidence;
    }
}
