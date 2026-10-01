using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;

namespace Knowledge.Application.Contracts;

/// <summary>Maps domain enums to the same words used in the documents' front matter.</summary>
public static class ContractVocabulary
{
    public static string ToApi(this DocumentStatus status) => status switch
    {
        DocumentStatus.Active => "active",
        DocumentStatus.Superseded => "superseded",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToApi(this DocumentCategory category) => category switch
    {
        DocumentCategory.Policy => "politika",
        DocumentCategory.Procedure => "prosedur",
        DocumentCategory.Guide => "kilavuz",
        DocumentCategory.Faq => "sss",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static string ToApi(this RetrievalMode mode) => mode == RetrievalMode.Hybrid ? "hybrid" : "lexical";
}
