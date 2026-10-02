using System.Globalization;
using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;
using Shared.Application.Common;

namespace Knowledge.Application.Answering;

/// <summary>
/// Deterministik sürüm çakışması çözümü. Arama aynı dokümanın (aynı <c>documentKey</c>) birden çok sürümünü
/// döndürdüğünde dil modeline yalnızca yürürlükteki sürüm verilir; diğerleri neden elendikleriyle birlikte raporlanır.
/// Bu kararın prompt'ta değil kodda verilmesi onu açıklanabilir ve test edilebilir kılar, modelin eski bir kuralı yanıta
/// karıştırmasını da imkânsız hâle getirir. Kural, ailesinde tek olan bir doküman için de geçerlidir: superseded
/// işaretli ya da henüz yürürlüğe girmemiş bir doküman asla kullanılmaz.
/// </summary>
/// <remarks>
/// "Bugün" enjekte edilen <c>TimeProvider</c> üzerinden okunur; testler sabit bir tarihle, örneğin ileri tarihli bir
/// sürümün henüz kullanılmadığını, deterministik olarak doğrulayabilir. Sınıf durumsuzdur ve tekil (singleton) olarak
/// kaydedilir.
/// </remarks>
public sealed class VersionResolver(TimeProvider timeProvider)
{
    /// <summary>
    /// Sürüm seçim kuralının Türkçe metni (<see cref="Messages.Answering.VersionRule"/>). Her yanıtta (retlerde de)
    /// <c>versionResolution.rule</c> alanında döner; istemci güncel sürümün hangi kurala göre seçildiğini yanıttan okuyabilir.
    /// </summary>
    public static string Rule => Messages.Answering.VersionRule;

    /// <summary>
    /// Arama adaylarını doküman ailesine göre gruplar, her ailenin yürürlükteki sürümünü seçer ve adayları buna göre ayırır:
    /// yürürlükteki sürümün bölümleri tutulur, diğer sürümlerin bölümleri gerekçeleriyle elenir.
    /// </summary>
    /// <remarks>
    /// Seçim aramada çıkan sürümlerle sınırlı değildir: <paramref name="versionsOf"/> indeksin sürüm kataloğunu verir. Böylece
    /// soruyla yalnızca eski sürüm eşleşmiş olsa bile ailenin güncel sürümü bilinir ve <c>NeedsSubstitution</c> ile
    /// handler'a bildirilir; handler o sürümün soruya en yakın bölümlerini eski bölümlerin yerine koyar. Tutulan adaylar
    /// özgün arama sırasını korur.
    /// </remarks>
    /// <param name="candidates">Sürüm çözümünden önceki arama sonuçları.</param>
    /// <param name="versionsOf">Bir aile anahtarı için indeksteki tüm sürümleri döndüren fonksiyon (soru akışında <c>IKnowledgeIndex.GetDocumentVersions</c>).</param>
    public VersionResolution Resolve(IReadOnlyList<SearchHit> candidates, Func<string, IReadOnlyList<DocumentVersion>> versionsOf)
    {
        // Yürürlük tarihleri takvim günüdür; "bugün" sunucunun yerel saat dilimine göre belirlenir.
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var families = new Dictionary<string, (IReadOnlyList<DocumentVersion> Versions, DocumentVersion? Current)>(StringComparer.Ordinal);

        foreach (var family in candidates.GroupBy(hit => hit.Chunk.DocumentKey, StringComparer.Ordinal))
        {
            var versions = versionsOf(family.Key);

            // Katalogda bir ailenin bulunmaması ancak arama ile bu çağrı arasında indeks yeniden kurulduysa olur; o durumda
            // ailenin sürümleri isabetlerin taşıdığı meta veriden çıkarılır.
            if (versions.Count == 0)
            {
                versions = family.Select(hit => ToVersion(hit.Chunk)).DistinctBy(version => version.DocumentId).ToList();
            }

            families[family.Key] = (versions, SelectCurrent(versions, today));
        }

        var kept = new List<SearchHit>();
        var discarded = new Dictionary<string, DiscardedVersion>(StringComparer.Ordinal);

        foreach (var hit in candidates)
        {
            var family = families[hit.Chunk.DocumentKey];

            if (hit.Chunk.DocumentId == family.Current?.DocumentId)
            {
                kept.Add(hit);
                continue;
            }

            // Aynı sürümün birden çok bölümü elenebilir; gerekçe sürüm başına bir kez kaydedilir.
            if (!discarded.ContainsKey(hit.Chunk.DocumentId))
            {
                var version = family.Versions.FirstOrDefault(candidate => candidate.DocumentId == hit.Chunk.DocumentId) ?? ToVersion(hit.Chunk);
                discarded[hit.Chunk.DocumentId] = new DiscardedVersion(version, Reason(version, family.Current, today));
            }
        }

        // Seçilen sürüm yalnızca gerçekten birden çok sürümü olan aileler için raporlanır; tek sürümlü ailede ortada bir
        // seçim yoktur. Elenen sürümler ise aile büyüklüğünden bağımsız olarak raporlanır.
        var selected = families.Values
            .Where(family => family.Versions.Count > 1 && family.Current is not null)
            .Select(family => family.Current!)
            .ToList();
        // Seçilen sürümün hiçbir bölümü aramada çıkmadıysa (soruyla yalnızca eski sürüm eşleştiyse) handler o sürümün
        // bölümlerini ayrıca getirmelidir; aksi hâlde model bu konuda hiç kaynak görmezdi.
        var needsSubstitution = selected.Where(version => kept.All(hit => hit.Chunk.DocumentId != version.DocumentId)).ToList();

        return new VersionResolution(kept, selected, discarded.Values.ToList(), needsSubstitution);
    }

    /// <summary>
    /// Bir ailenin yürürlükteki sürümünü seçer: superseded olmayan ve yürürlük tarihi bugün veya daha önce olan sürümler
    /// arasından en yeni tarihli olanı; tarih eşitse sürüm numarası büyük olanı. Uygun sürüm yoksa null döner.
    /// </summary>
    /// <remarks>
    /// Sürüm numaraları metin olarak değil <c>System.Version</c> olarak karşılaştırılır; aksi hâlde "1.10" metin sıralamasında
    /// "1.9"un gerisinde kalırdı. Ayrıştırılamayan sürüm 0.0 sayılır. Null sonuç ailenin hiçbir sürümünün kullanılamayacağı
    /// anlamına gelir; ailenin tüm bölümleri elenir.
    /// </remarks>
    private static DocumentVersion? SelectCurrent(IReadOnlyList<DocumentVersion> versions, DateOnly today)
    {
        return versions
            .Where(version => version.Status != DocumentStatus.Superseded && version.EffectiveDate <= today)
            .OrderByDescending(version => version.EffectiveDate)
            .ThenByDescending(version => System.Version.TryParse(version.Version, out var number) ? number : new System.Version(0, 0))
            .FirstOrDefault();
    }

    /// <summary>
    /// Elenen bir sürüm için yanıtta gösterilecek Türkçe gerekçeyi üretir: sürüm henüz yürürlüğe girmemişse yürürlük
    /// tarihini, ailede yürürlükte bir sürüm yoksa bunu, aksi hâlde onu geçersiz kılan güncel sürümü ve tarihini belirtir.
    /// </summary>
    /// <remarks>
    /// Gerekçe, "kaynaklar çeliştiğinde güncel sürümün nasıl seçildiğini göster" şartını karşılar: istemci hangi sürümün
    /// neden kullanılmadığını doğrudan yanıttan okur.
    /// </remarks>
    private static string Reason(DocumentVersion version, DocumentVersion? current, DateOnly today)
    {
        if (version.EffectiveDate > today)
        {
            return string.Format(CultureInfo.InvariantCulture, Messages.Answering.NotYetInEffect, Format(version.EffectiveDate));
        }

        return current is null
            ? Messages.Answering.NoVersionInEffect
            : string.Format(CultureInfo.InvariantCulture, Messages.Answering.SupersededBy, current.Version, Format(current.EffectiveDate));
    }

    /// <summary>
    /// Bir indeks bölümünün taşıdığı meta veriden sürüm kaydı oluşturur. Katalogda bulunamayan sürümler için yedektir
    /// (indeks, arama ile sürüm çözümü arasında yeniden kurulduğunda).
    /// </summary>
    private static DocumentVersion ToVersion(IndexedChunk chunk) =>
        new(chunk.DocumentId, chunk.DocumentKey, chunk.Title, chunk.Version, chunk.EffectiveDate, chunk.Status, chunk.Category);

    /// <summary>
    /// Tarihi kültürden bağımsız ISO biçiminde (yyyy-MM-dd) yazar; gerekçe metni sunucunun bölge ayarına göre değişmez ve
    /// front matter'daki tarih biçimiyle aynı kalır.
    /// </summary>
    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// Sürüm çözümünün sonucu: modele gidebilecek adaylar, seçilen ve elenen sürümler ve bölümleri sonradan getirilmesi
/// gereken güncel sürümler.
/// </summary>
/// <param name="Kept">Yürürlükteki sürümlerden gelen adaylar, özgün sıralarıyla.</param>
/// <param name="Selected">Adaylar arasında görünen her çok sürümlü aile için seçilen sürüm.</param>
/// <param name="Discarded">Adaylardan çıkarılan sürümler ve gerekçeleri.</param>
/// <param name="NeedsSubstitution">Hiçbir bölümü aramada çıkmamış seçili sürümler; bunların en iyi bölümleri ayrıca getirilmelidir.</param>
public sealed record VersionResolution(
    IReadOnlyList<SearchHit> Kept,
    IReadOnlyList<DocumentVersion> Selected,
    IReadOnlyList<DiscardedVersion> Discarded,
    IReadOnlyList<DocumentVersion> NeedsSubstitution)
{
    /// <summary>En az bir sürüm elendiyse true; yani sürüm kuralı bu soruda modele gidecek bağlamı fiilen değiştirdi.</summary>
    public bool Applied => Discarded.Count > 0;
}

/// <summary>
/// Elenen bir sürüm ve neden kullanılmadığının Türkçe açıklaması; yanıtta <c>versionResolution.discarded</c> altında
/// gösterilir (yalnızca yanıtın atıf yaptığı aileler için).
/// </summary>
/// <param name="Version">Elenen sürüm.</param>
/// <param name="Reason">Türkçe gerekçe, ör. "2.0 sürümü (2025-06-01) tarafından geçersiz kılındı."</param>
public sealed record DiscardedVersion(DocumentVersion Version, string Reason);
