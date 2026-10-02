using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;

namespace Knowledge.Application.Answering;

/// <summary>İki farklı doküman birbiriyle çeliştiğinde hangi kaynağın kazandığını belirleyen kural.</summary>
/// <remarks>
/// Aynı doküman ailesinin sürümlerini <c>VersionResolver</c> model çağrılmadan çözer; bu sınıf farklı aileler arasındaki
/// çelişkiler içindir (ör. güncel iade politikası ile eski bilgiyi tekrarlayan SSS). Kural prompt'ta modele de verilir;
/// sunucu, modelin bildirdiği her çelişkide seçimin bu kurala uyup uymadığını hesaplar ve <c>ruleSatisfied</c> olarak
/// raporlar. Kuralın ihlali yanıtı reddettirmez, yalnızca görünür kılar; çelişkiyi fark edip bildirmek ise hâlâ modele bağlıdır.
/// </remarks>
public static class SourcePrecedence
{
    /// <summary>
    /// Öncelik kuralının Türkçe, insan tarafından okunabilir ifadesi; system prompt'taki 5. kuralla aynı önceliği anlatır.
    /// </summary>
    public const string Rule = "Politika ve prosedür dokümanları kılavuzlardan, kılavuzlar SSS'den önceliklidir; aynı türde yürürlük tarihi daha yeni olan geçerlidir.";

    /// <summary>
    /// <paramref name="candidate"/> kaynağı <paramref name="other"/> kaynağına üstün geliyorsa true döndürür: önce yetki
    /// sırası (politika/prosedür &gt; kılavuz &gt; SSS), yetki eşitse yürürlük tarihi daha yeni olan.
    /// </summary>
    /// <remarks>
    /// Yetki tazelikten önce gelir: daha yeni tarihli bir SSS, daha eski tarihli bir politikayı geçersiz kılamaz, çünkü SSS
    /// politikanın bir özetidir ve onun gerisinde kalabilir. Yetki ve tarih eşitse aday kazanmış sayılır (≥): kuralın
    /// ayırt edemediği eşit kaynaklar arasında modelin seçimi ihlal olarak işaretlenmez.
    /// </remarks>
    /// <param name="candidate">Modelin seçtiği kaynak.</param>
    /// <param name="other">Modelin elediği kaynaklardan biri.</param>
    public static bool Outranks(IndexedChunk candidate, IndexedChunk other)
    {
        var candidateRank = AuthorityRank(candidate.Category);
        var otherRank = AuthorityRank(other.Category);

        return candidateRank != otherRank
            ? candidateRank < otherRank
            : candidate.EffectiveDate >= other.EffectiveDate;
    }

    /// <summary>
    /// Doküman türünün yetki sırası; küçük değer daha yetkilidir. Politikayı politika sahibi onaylar, SSS ise politikanın
    /// gerisinde kalabilecek bir özettir.
    /// </summary>
    /// <remarks>
    /// Politika ve prosedür aynı seviyededir (0): ikisi de onaylı kural metnidir ve aralarındaki çelişkiyi tarih belirler.
    /// SSS ve ileride eklenebilecek tanımsız türler en düşük yetkiyi (2) alır; böylece yeni bir tür yanlışlıkla politikadan
    /// üstün sayılmaz.
    /// </remarks>
    private static int AuthorityRank(DocumentCategory category) => category switch
    {
        DocumentCategory.Policy or DocumentCategory.Procedure => 0,
        DocumentCategory.Guide => 1,
        _ => 2
    };
}
