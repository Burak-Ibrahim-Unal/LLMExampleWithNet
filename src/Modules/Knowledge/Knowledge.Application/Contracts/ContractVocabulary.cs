using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;

namespace Knowledge.Application.Contracts;

/// <summary>
/// Domain enum'larını, dokümanların front matter'ında kullanılan sözcüklere çevirir; API'nin kararlı dize sözlüğüdür.
/// </summary>
/// <remarks>
/// Enum adları (<c>Policy</c>, <c>Faq</c>…) iç ayrıntıdır; yeniden adlandırılmaları ya da sıralarının değişmesi API
/// sözleşmesini bozmamalıdır. Bu yüzden enum'un adı veya sayısal değeri yerine burada açıkça eşlenen sabit dizeler döner.
/// Front matter'daki sözcüklerin (<c>politika</c>, <c>prosedur</c>, <c>kilavuz</c>, <c>sss</c>, <c>active</c>,
/// <c>superseded</c>) kullanılması, doküman yazarının, API istemcisinin ve prompt'taki kaynak başlığını okuyan modelin
/// aynı sözlüğü görmesini sağlar. Tanınmayan bir enum değeri belgelenmemiş bir dize üretmek yerine istisna fırlatır.
/// </remarks>
public static class ContractVocabulary
{
    /// <summary>Doküman durumunu API sözcüğüne çevirir: "active" veya "superseded".</summary>
    public static string ToApi(this DocumentStatus status) => status switch
    {
        DocumentStatus.Active => "active",
        DocumentStatus.Superseded => "superseded",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    /// <summary>Doküman türünü front matter sözcüğüne çevirir: "politika", "prosedur", "kilavuz" veya "sss".</summary>
    /// <remarks>
    /// Aynı sözcük prompt'taki kaynak başlığında da (<c>tür: politika</c>) kullanılır; model öncelik kuralını uygularken
    /// kaynağın türünü buradan okur.
    /// </remarks>
    public static string ToApi(this DocumentCategory category) => category switch
    {
        DocumentCategory.Policy => "politika",
        DocumentCategory.Procedure => "prosedur",
        DocumentCategory.Guide => "kilavuz",
        DocumentCategory.Faq => "sss",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    /// <summary>
    /// Arama modunu API sözcüğüne çevirir: "hybrid" veya "lexical". Bunlar <c>/v1/search</c> ucunun <c>mode</c>
    /// parametresinde kabul edilen değerlerle aynıdır; hibrit dışındaki her değer "lexical" sayılır.
    /// </summary>
    public static string ToApi(this RetrievalMode mode) => mode == RetrievalMode.Hybrid ? "hybrid" : "lexical";
}
