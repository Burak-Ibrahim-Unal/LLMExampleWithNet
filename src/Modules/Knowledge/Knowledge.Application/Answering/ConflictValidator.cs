using System.Globalization;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Contracts;
using Shared.Application.Common;

namespace Knowledge.Application.Answering;

/// <summary>
/// Modelin bildirdiği kaynaklar arası çelişkileri sunucu tarafında denetler: kayıtları modele verilen bölümlerle eşler
/// ve her çelişkide öncelik kuralına (<see cref="SourcePrecedence"/>) göre kaybedenleri hesaplar. Handler kuralı bu
/// sonuca göre zorlar (kaybedenleri bağlamdan çıkarıp modeli yeniden çağırır).
/// </summary>
/// <remarks>
/// Atıfların denetimi <see cref="CitationValidator"/>'dadır; bu sınıf onun çelişkiler için olan karşılığıdır. Aynı
/// doküman ailesinin sürümleri arasındaki seçim burada değil, model çağrılmadan önce <see cref="VersionResolver"/>
/// tarafından yapılır.
/// </remarks>
internal static class ConflictValidator
{
    /// <summary>
    /// Modelin bildirdiği, farklı dokümanlar arasındaki çelişkileri sunucu tarafında doğrular. Her çelişki için kurala
    /// (<see cref="SourcePrecedence"/>: politika/prosedür &gt; kılavuz &gt; SSS, eşit yetkide daha yeni yürürlük tarihi)
    /// göre kaybeden kaynaklar ve modelin seçiminin kurala uyup uymadığı hesaplanır. Verilen bağlamda olmayan kimlikler
    /// ayrıca sayılır.
    /// </summary>
    /// <remarks>
    /// Görev, kaynaklar çeliştiğinde güncel olanın nasıl seçildiğinin gösterilmesini istiyor; bunu yalnızca modelin
    /// beyanına bırakmak, modelin yanlış kaynağı seçtiği durumları gizlerdi. Model etiketleri farklı biçimlerde
    /// yazabildiği için ("c2", "[C2]", "2") etiketler önce normalize edilir, seçilen kaynak reddedilenler arasında sayılmaz
    /// ve tekrarlar ayıklanır. Bağlamda olmayan bir kimlik (seçilen ya da elenen) sessizce yutulmaz: çelişki geçersiz
    /// kimlik içerir diye sayılır ve handler bunu düzeltme turuna, sürerse <c>UnresolvedConflict</c> reddine götürür. Seçilen
    /// kaynağı ve en az bir elenen kaynağı bağlamda olan çelişkiler yine de denetlenir; kuralı zorlamak için bu kısım
    /// yeterlidir. Aynı belgenin sürümleri arasındaki seçim burada değil, model çağrılmadan önce
    /// <see cref="VersionResolver"/> tarafından yapılır.
    /// </remarks>
    /// <param name="conflicts">Modelin bildirdiği çelişkiler.</param>
    /// <param name="context">Modele bu denemede verilen etiketli bölümler.</param>
    /// <returns>Denetlenebilen çelişkiler ve geçersiz kimlik içeren çelişki sayısı.</returns>
    public static (IReadOnlyList<CheckedConflict> Conflicts, int InvalidReferences) Validate(IReadOnlyList<GeneratedConflict> conflicts, IReadOnlyList<ContextChunk> context)
    {
        var sourcesByLabel = context.ToDictionary(source => source.Label, StringComparer.OrdinalIgnoreCase);
        var checkedConflicts = new List<CheckedConflict>();
        var invalidReferences = 0;

        foreach (var conflict in conflicts)
        {
            if (!sourcesByLabel.TryGetValue(SourceLabel.Normalize(conflict.ChosenChunkLabel), out var chosen))
            {
                invalidReferences++;
                continue;
            }

            var rejectedLabels = conflict.RejectedChunkLabels
                .Select(SourceLabel.Normalize)
                .Distinct()
                .Where(label => label != chosen.Label)
                .ToList();
            var rejected = rejectedLabels
                .Where(sourcesByLabel.ContainsKey)
                .Select(label => sourcesByLabel[label])
                .ToList();

            if (rejected.Count < rejectedLabels.Count || rejected.Count == 0)
            {
                invalidReferences++;
            }

            if (rejected.Count > 0)
            {
                checkedConflicts.Add(new CheckedConflict(conflict.Topic.Trim(), chosen, rejected, conflict.Reason.Trim()));
            }
        }

        return (checkedConflicts, invalidReferences);
    }
}

/// <summary>
/// Sunucuda doğrulanmış bir çelişki: modelin seçtiği ve elediği bağlam bölümleri ile bunlardan kurala göre
/// kaybedenler.
/// </summary>
/// <remarks>
/// API'deki <see cref="ConflictDto"/> doküman kimlikleri taşır; handler'ın ise kuralı zorlamak için bağlam bölümlerine
/// (hangi bölüm çıkarılacak, hangi atıf kaybedene işaret ediyor) ihtiyacı vardır. Bu kayıt ikisini birleştirir ve API
/// biçimine yanıtın sonunda çevrilir.
/// </remarks>
/// <param name="Topic">Modelin bildirdiği çelişki konusu.</param>
/// <param name="Chosen">Modelin geçerli kabul ettiği bölüm.</param>
/// <param name="Rejected">Modelin elediği bölümler (bağlamda olanlar, tekrarsız).</param>
/// <param name="Reason">Modelin seçim gerekçesi.</param>
internal sealed record CheckedConflict(string Topic, ContextChunk Chosen, IReadOnlyList<ContextChunk> Rejected, string Reason)
{
    /// <summary>Çelişkinin tüm üyeleri: önce seçilen, ardından elenen bölümler.</summary>
    private IReadOnlyList<ContextChunk> Members { get; } = [Chosen, .. Rejected];

    /// <summary>
    /// Kurala göre kaybeden bölümler (<see cref="SourcePrecedence.Losers"/>). Düzeltme turunda bağlamdan çıkarılırlar;
    /// yanıtın kabul edilen bir atfı bunlardan birine işaret ediyorsa yanıt da ihlal sayılır.
    /// </summary>
    public IReadOnlyList<ContextChunk> Losers { get; } = SourcePrecedence.Losers([Chosen, .. Rejected]);

    /// <summary>
    /// Modelin seçimi kurala uyuyorsa true: seçilen bölüm kaybedenler arasında değildir. Eşit yetki ve eşit tarihte
    /// kural kaynakları ayırt edemediğinden modelin seçimi kurala uygun sayılır.
    /// </summary>
    public bool RuleSatisfied => !Losers.Contains(Chosen);

    /// <summary>
    /// Kurala göre geçerli kaynak: model kurala uyduysa seçtiği bölüm, uymadıysa kaybedenler dışındaki ilk üye (eşitlikte
    /// modelin sıralaması korunur). Yanıtın bu kaynağın dokümanına atıf yapması beklenir; yapmıyorsa çelişki beyanı ile
    /// yanıt birbirini tutmaz.
    /// </summary>
    public ContextChunk Winner => Members.First(member => !Losers.Contains(member));

    /// <summary>Çelişkiyi modelin bildirdiği hâliyle, sunucunun <c>RuleSatisfied</c> kararıyla birlikte API biçimine çevirir.</summary>
    public ConflictDto ToDto() =>
        new(Topic, ToConflictSource(Chosen), Rejected.Select(ToConflictSource).ToList(), Reason, RuleSatisfied);

    /// <summary>
    /// Düzeltme turundan önce, sunucunun kararını gösteren çelişki kaydını üretir. Model kurala uyduysa kayıt modelin
    /// kaydıdır; uymadıysa seçilen kaynak kuralın kazananı, elenenler kaybedenler, gerekçe sunucunun kuralı
    /// uyguladığını söyleyen metindir (<see cref="Messages.Answering.PrecedenceEnforced"/>) ve <c>RuleSatisfied</c> true olur.
    /// </summary>
    /// <remarks>
    /// Kaybeden bölümler bağlamdan çıkarıldığı için model ikinci denemede bu çelişkiyi bir daha göremez ve bildiremez.
    /// Kayıt saklanmasaydı yanıt, kaynakların çeliştiğini ve güncel olanın nasıl seçildiğini göstermezdi.
    /// </remarks>
    public ConflictDto ToEnforcedDto()
    {
        if (RuleSatisfied)
        {
            return ToDto();
        }

        return new ConflictDto(
            Topic,
            ToConflictSource(Winner),
            Losers.Select(ToConflictSource).ToList(),
            string.Format(CultureInfo.InvariantCulture, Messages.Answering.PrecedenceEnforced, SourcePrecedence.Rule),
            RuleSatisfied: true);
    }

    /// <summary>
    /// Bir çelişkide seçilen ya da reddedilen kaynağı, öncelik kararını denetlemeye yetecek alanlarla (doküman, sürüm,
    /// yürürlük tarihi, tür, bölüm) temsil eder. Tür ve tarih bilerek dahil edilir: <c>RuleSatisfied</c> kararı tam da bu
    /// iki alana dayanır ve okuyan kişi kararı kendisi de kontrol edebilmelidir.
    /// </summary>
    private static ConflictSourceDto ToConflictSource(ContextChunk source) =>
        new(source.Chunk.DocumentId, source.Chunk.Version, source.Chunk.EffectiveDate, source.Chunk.Category.ToApi(), source.Chunk.SectionPath);
}
