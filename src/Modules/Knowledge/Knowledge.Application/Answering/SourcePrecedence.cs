using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;
using Shared.Application.Common;

namespace Knowledge.Application.Answering;

/// <summary>İki farklı doküman birbiriyle çeliştiğinde hangi kaynağın kazandığını belirleyen kural.</summary>
/// <remarks>
/// Aynı doküman ailesinin sürümlerini <c>VersionResolver</c> model çağrılmadan çözer; bu sınıf farklı aileler arasındaki
/// çelişkiler içindir (ör. güncel iade politikası ile eski bilgiyi tekrarlayan SSS). Kural prompt'ta modele de verilir;
/// sunucu, modelin bildirdiği her çelişkide seçimin bu kurala uyup uymadığını hesaplar (<c>ruleSatisfied</c>) ve kuralı
/// zorlar: model kaybeden kaynağı seçtiyse ya da yanıtını ona dayandırdıysa handler kaybedenleri (<see cref="Losers"/>)
/// bağlamdan çıkarıp modeli bir kez daha çağırır, ihlal sürerse yanıtı reddeder. Çelişkiyi fark edip bildirmek ise hâlâ
/// modele bağlıdır; bildirilmeyen bir çelişkiyi sunucu göremez.
/// </remarks>
public static class SourcePrecedence
{
    /// <summary>
    /// Öncelik kuralının Türkçe, insan tarafından okunabilir ifadesi (<see cref="Messages.Answering.PrecedenceRule"/>);
    /// system prompt'taki 5. kuralla aynı önceliği anlatır.
    /// Sunucu kuralı zorladığında (modelin seçimi düzeltildiğinde) çelişki kaydının gerekçesine de bu metin yazılır;
    /// kararın hangi kurala dayandığı yanıtın içinden okunur.
    /// </summary>
    public static string Rule => Messages.Answering.PrecedenceRule;

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
    /// Bir çelişkinin üyeleri arasından kurala göre kaybedenleri döndürür: başka bir üye tarafından kesin olarak geçilen
    /// her kaynak. Sonuç üyelerin verildiği sırayı korur.
    /// </summary>
    /// <remarks>
    /// "Kesin olarak geçmek", kuralın iki kaynağı ayırt edebilmesi demektir: biri diğerine üstün gelir ama tersi doğru
    /// değildir. Aynı yetki düzeyinde ve aynı tarihteki kaynaklar birbirini geçemez; kural onları ayırt edemediği için
    /// hiçbiri kaybeden sayılmaz ve modelin seçimi kabul edilir. Handler kaybedenleri bağlamdan çıkarıp modeli yeniden
    /// çağırdığı için bu küme kesin olmalıdır: kazananı kaybeden saymak güncel kuralı bağlamdan silerdi.
    /// </remarks>
    /// <param name="members">Çelişkinin seçilen ve elenen kaynakları.</param>
    public static IReadOnlyList<ContextChunk> Losers(IReadOnlyList<ContextChunk> members) =>
        members.Where(member => members.Any(other => Beats(other.Chunk, member.Chunk))).ToList();

    /// <summary>
    /// <paramref name="candidate"/> kaynağı <paramref name="other"/> kaynağını kesin olarak geçiyorsa true döndürür:
    /// <see cref="Outranks"/> bir yönde doğru, diğer yönde yanlıştır.
    /// </summary>
    /// <remarks>
    /// <see cref="Outranks"/> eşitlikte iki yönde de true döndürür (modelin seçimi eşitlikte ihlal sayılmasın diye); bir
    /// kaynağı bağlamdan çıkarmak gibi geri dönüşü olan bir karar için eşitlik yetmez, kesin üstünlük gerekir.
    /// </remarks>
    private static bool Beats(IndexedChunk candidate, IndexedChunk other) =>
        Outranks(candidate, other) && !Outranks(other, candidate);

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
