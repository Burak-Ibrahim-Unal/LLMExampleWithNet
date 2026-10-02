using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Domain.Entities;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Answering;

/// <summary>
/// <see cref="VersionResolver"/> için birim testleri. Çözümleyici, aynı doküman ailesinin (aynı <c>documentKey</c>)
/// adayları arasından yalnızca yürürlükteki sürümü (yürürlük tarihi bugün veya daha önce olan, <c>superseded</c>
/// işaretli olmayan en yeni sürüm) bırakır; diğerlerini Türkçe gerekçeleriyle raporlar.
/// </summary>
/// <remarks>
/// "Bugün" <see cref="FixedTimeProvider"/> ile 2026-10-01'e sabitlenir; böylece 2027 tarihli "gelecek" sürüm testleri
/// gerçek takvim ilerledikçe kendiliğinden kırılmaz. Sürüm kataloğu, <c>IKnowledgeIndex.GetDocumentVersions</c> yerine
/// geçen basit bir temsilciyle (delegate) verilir. Gerekçe metinleri API yanıtında kullanıcıya gösterildiği için birebir
/// karşılaştırılır.
/// </remarks>
public sealed class VersionResolverTests
{
    /// <summary>İade politikasının eski sürümü: 1.0, 2024-01-15, <c>superseded</c>.</summary>
    private static readonly DocumentVersion ReturnsV1 = Version("iade-v1", "iade", "1.0", new DateOnly(2024, 1, 15), DocumentStatus.Superseded);
    /// <summary>İade politikasının güncel sürümü: 2.0, 2025-06-01, <c>active</c>.</summary>
    private static readonly DocumentVersion ReturnsV2 = Version("iade-v2", "iade", "2.0", new DateOnly(2025, 6, 1), DocumentStatus.Active);
    /// <summary>
    /// Tek sürümlü, yürürlükteki ayrı bir aile (kargo); sürüm kararlarının ilgisiz ailelere dokunmadığını göstermek için
    /// adaylara eklenir.
    /// </summary>
    private static readonly DocumentVersion Shipping = Version("kargo", "kargo", "1.0", new DateOnly(2025, 3, 1), DocumentStatus.Active);

    /// <summary>
    /// Bir sürüm kaydı oluşturur. Tür her zaman politikadır, çünkü sürüm çözümleme türe bakmaz; tür yalnızca farklı
    /// dokümanlar arasındaki öncelikte (<c>SourcePrecedence</c>) önemlidir.
    /// </summary>
    private static DocumentVersion Version(string id, string key, string version, DateOnly effectiveDate, DocumentStatus status) =>
        new(id, key, $"Belge {key}", version, effectiveDate, status, DocumentCategory.Policy);

    /// <summary>
    /// Verilen sürümün bir bölümü için arama isabeti (<c>SearchHit</c>) oluşturur. Skorlar sabittir, çünkü
    /// <c>VersionResolver</c> skorlara bakmaz; yalnızca adayın aile, kimlik, sürüm, tarih ve durum bilgisini kullanır.
    /// </summary>
    private static SearchHit Hit(DocumentVersion version, string section = "Bölüm") =>
        new(new IndexedChunk(Guid.NewGuid(), version.DocumentId, version.DocumentKey, version.Title, version.Version,
            version.EffectiveDate, version.Status, version.Category, section, "metin"), 0.03, 1.0, 1.0, null);

    /// <summary>
    /// Çözümleyiciyi sabit "bugün" (2026-10-01) ile çalıştırır.
    /// </summary>
    /// <param name="candidates">Aramadan gelen adaylar, arama sırasıyla.</param>
    /// <param name="catalog">
    /// İndeksin bildiği tüm sürümler; aramada çıkmamış sürümleri de içerebilir. Güncel sürümün aramada hiç çıkmadığı
    /// durum (yerine koyma ihtiyacı) ancak böyle sınanabilir.
    /// </param>
    private static VersionResolution Resolve(IReadOnlyList<SearchHit> candidates, params DocumentVersion[] catalog)
    {
        var resolver = new VersionResolver(new FixedTimeProvider(new DateOnly(2026, 10, 1)));
        return resolver.Resolve(candidates, key => catalog.Where(version => version.DocumentKey == key).ToList());
    }

    /// <summary>
    /// Temel senaryo: aramada iade politikasının iki sürümü ve kargo dokümanı çıkar. Güncel 2.0 sürümü ve kargo, arama
    /// sırası korunarak tutulur; 1.0 sürümü "2.0 sürümü (2025-06-01) tarafından geçersiz kılındı." gerekçesiyle elenir;
    /// seçilen sürüm 2.0 olarak raporlanır ve <c>Applied=true</c> olur. Güncel sürüm zaten aramada bulunduğu için yerine
    /// koyma gerekmez.
    /// </summary>
    /// <remarks>
    /// "Kaynaklar çeliştiğinde güncel sürümün nasıl seçildiğini göster" gereksiniminin çekirdeğidir. Bu test kırılırsa
    /// eski sürüm modele giden adaylarda kalabilir ya da kullanıcı neden elendiğini göremez.
    /// </remarks>
    [Fact]
    public void Keeps_the_newest_version_in_effect_and_discards_the_older_one_with_a_reason()
    {
        var resolution = Resolve([Hit(ReturnsV1), Hit(ReturnsV2), Hit(Shipping)], ReturnsV1, ReturnsV2, Shipping);

        resolution.Kept.Select(hit => hit.Chunk.DocumentId).ShouldBe(["iade-v2", "kargo"]);
        resolution.Selected.ShouldBe([ReturnsV2]);
        resolution.Discarded.ShouldHaveSingleItem().Version.ShouldBe(ReturnsV1);
        resolution.Discarded[0].Reason.ShouldBe("2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.");
        resolution.NeedsSubstitution.ShouldBeEmpty();
        resolution.Applied.ShouldBeTrue();
    }

    /// <summary>
    /// Durumu <c>active</c> olsa bile yürürlük tarihi gelmemiş bir sürümün (3.0, 2027-01-01) kullanılmadığını, bugün
    /// yürürlükte olan 2.0 sürümünün tutulduğunu doğrular; elenen sürümün gerekçesi "Yürürlük tarihi (2027-01-01) henüz
    /// gelmedi." olur. Aramada çıkmayan 1.0 sürümü raporlanmaz: yalnızca aday olarak gelen sürümler hakkında karar
    /// bildirilir.
    /// </summary>
    /// <remarks>
    /// Yeni bir politika önceden yayımlanabilir; yürürlüğe girmeden uygulanması müşteriye henüz geçerli olmayan bir kuralı
    /// anlatmak olur. Bu yüzden durum (status) tek başına yeterli değildir, tarih de kontrol edilir.
    /// </remarks>
    [Fact]
    public void A_version_whose_effective_date_has_not_come_yet_is_not_used()
    {
        var returnsV3 = Version("iade-v3", "iade", "3.0", new DateOnly(2027, 1, 1), DocumentStatus.Active);

        var resolution = Resolve([Hit(returnsV3), Hit(ReturnsV2)], ReturnsV1, ReturnsV2, returnsV3);

        resolution.Kept.Select(hit => hit.Chunk.DocumentId).ShouldBe(["iade-v2"]);
        resolution.Discarded.ShouldHaveSingleItem().Reason.ShouldBe("Yürürlük tarihi (2027-01-01) henüz gelmedi.");
    }

    /// <summary>
    /// Soru yalnızca eski sürümle (1.0) eşleştiğinde eski sürüm elenir, hiçbir aday tutulmaz ve güncel 2.0 sürümü
    /// <c>NeedsSubstitution</c> listesine eklenir; handler bu listeye bakarak güncel sürümün en iyi bölümlerini ayrıca
    /// getirir ve elenen bölümlerin yerine koyar.
    /// </summary>
    /// <remarks>
    /// Kullanıcı eski dokümanın ifadeleriyle soru sorabilir. Yerine koyma olmasaydı böyle bir soru, yanıtı güncel sürümde
    /// bulunduğu hâlde reddedilirdi; doğru davranış, soruyu güncel sürümün yanıtlamasıdır.
    /// </remarks>
    [Fact]
    public void When_only_an_outdated_version_matched_the_current_version_is_requested_instead()
    {
        var resolution = Resolve([Hit(ReturnsV1)], ReturnsV1, ReturnsV2);

        resolution.Kept.ShouldBeEmpty();
        resolution.Discarded.ShouldHaveSingleItem().Version.ShouldBe(ReturnsV1);
        resolution.NeedsSubstitution.ShouldBe([ReturnsV2]);
    }

    /// <summary>
    /// Tek sürümlü ve yürürlükte olan bir dokümanın (kargo) olduğu gibi tutulduğunu ve hakkında hiçbir sürüm kararı
    /// raporlanmadığını (<c>Selected</c> ve <c>Discarded</c> boş, <c>Applied=false</c>) doğrular.
    /// </summary>
    /// <remarks>
    /// Sürüm kararları yalnızca birden çok sürümü olan aileler için anlamlıdır; her tek sürümlü doküman için "seçildi"
    /// raporlamak <c>versionResolution</c> alanını gürültüyle doldururdu.
    /// </remarks>
    [Fact]
    public void Documents_with_a_single_version_pass_through_untouched()
    {
        var resolution = Resolve([Hit(Shipping)], Shipping);

        resolution.Kept.Select(hit => hit.Chunk.DocumentId).ShouldBe(["kargo"]);
        resolution.Selected.ShouldBeEmpty();
        resolution.Discarded.ShouldBeEmpty();
        resolution.Applied.ShouldBeFalse();
    }

    /// <summary>
    /// Katalogda yalnızca <c>superseded</c> işaretli 1.0 sürümü varken (ardılı indekste yok; ör. dosyası silinmiş) bu
    /// sürümün kullanılmadığını doğrular: aday tutulmaz, gerekçe "Bu dokümanın yürürlükte bir sürümü yok." olur ve yerine
    /// konacak bir sürüm de yoktur.
    /// </summary>
    /// <remarks>
    /// Kural tek sürümlü aileler için de geçerlidir: geçersiz kılınmış bir doküman, yerini alacak sürüm bulunmasa bile
    /// asla modele verilmez. Başka aday kalmadığında handler bu durumu <c>NoSourceInEffect</c> ile reddeder.
    /// </remarks>
    [Fact]
    public void A_superseded_document_is_not_used_even_when_its_successor_is_missing()
    {
        var resolution = Resolve([Hit(ReturnsV1)], ReturnsV1);

        resolution.Kept.ShouldBeEmpty();
        resolution.Discarded.ShouldHaveSingleItem().Reason.ShouldBe("Bu dokümanın yürürlükte bir sürümü yok.");
        resolution.NeedsSubstitution.ShouldBeEmpty();
    }

    /// <summary>
    /// Tek sürümlü bir dokümanın (2027-01-01'de yürürlüğe girecek garanti dokümanı) yürürlük tarihinden önce
    /// kullanılmadığını, aynı aramadaki ilgisiz kargo dokümanının ise tutulduğunu doğrular; gerekçe "Yürürlük tarihi
    /// (2027-01-01) henüz gelmedi." olur.
    /// </summary>
    /// <remarks>
    /// Ailesinde başka sürüm olmaması gelecek tarihli bir dokümanı yürürlüğe sokmaz; tarih kuralı tek sürümlü aileler için
    /// de geçerlidir.
    /// </remarks>
    [Fact]
    public void A_single_document_is_not_used_before_its_effective_date()
    {
        var upcoming = Version("garanti-2027", "garanti", "1.0", new DateOnly(2027, 1, 1), DocumentStatus.Active);

        var resolution = Resolve([Hit(upcoming), Hit(Shipping)], upcoming, Shipping);

        resolution.Kept.Select(hit => hit.Chunk.DocumentId).ShouldBe(["kargo"]);
        resolution.Discarded.ShouldHaveSingleItem().Reason.ShouldBe("Yürürlük tarihi (2027-01-01) henüz gelmedi.");
    }

    /// <summary>
    /// Aynı yürürlük tarihine sahip iki sürümden ("1.9" ve "1.10") sürüm numarası sayısal olarak büyük olanın (1.10)
    /// seçildiğini ve diğerinin "1.10 sürümü (2025-01-01) tarafından geçersiz kılındı." gerekçesiyle elendiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Eşitlik, sürüm metni <c>System.Version</c> olarak ayrıştırılarak bozulur. Metin karşılaştırması "1.9" değerini
    /// "1.10"dan büyük sayar ve eski sürümü seçerdi; bu test o hatayı yakalar.
    /// </remarks>
    [Fact]
    public void Versions_with_the_same_effective_date_are_ordered_by_version_number()
    {
        var older = Version("garanti-1-9", "garanti", "1.9", new DateOnly(2025, 1, 1), DocumentStatus.Active);
        var newer = Version("garanti-1-10", "garanti", "1.10", new DateOnly(2025, 1, 1), DocumentStatus.Active);

        var resolution = Resolve([Hit(older), Hit(newer)], older, newer);

        resolution.Selected.ShouldBe([newer]);
        resolution.Discarded.ShouldHaveSingleItem().Reason.ShouldBe("1.10 sürümü (2025-01-01) tarafından geçersiz kılındı.");
    }
}
