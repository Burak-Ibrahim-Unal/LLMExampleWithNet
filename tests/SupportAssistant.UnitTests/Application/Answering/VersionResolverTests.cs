using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Domain.Entities;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Answering;

public sealed class VersionResolverTests
{
    private static readonly DocumentVersion ReturnsV1 = Version("iade-v1", "iade", "1.0", new DateOnly(2024, 1, 15), DocumentStatus.Superseded);
    private static readonly DocumentVersion ReturnsV2 = Version("iade-v2", "iade", "2.0", new DateOnly(2025, 6, 1), DocumentStatus.Active);
    private static readonly DocumentVersion Shipping = Version("kargo", "kargo", "1.0", new DateOnly(2025, 3, 1), DocumentStatus.Active);

    private static DocumentVersion Version(string id, string key, string version, DateOnly effectiveDate, DocumentStatus status) =>
        new(id, key, $"Belge {key}", version, effectiveDate, status, DocumentCategory.Policy);

    private static SearchHit Hit(DocumentVersion version, string section = "Bölüm") =>
        new(new IndexedChunk(Guid.NewGuid(), version.DocumentId, version.DocumentKey, version.Title, version.Version,
            version.EffectiveDate, version.Status, version.Category, section, "metin"), 0.03, 1.0, 1.0, null);

    private static VersionResolution Resolve(IReadOnlyList<SearchHit> candidates, params DocumentVersion[] catalog)
    {
        var resolver = new VersionResolver(new FixedTimeProvider(new DateOnly(2026, 10, 1)));
        return resolver.Resolve(candidates, key => catalog.Where(version => version.DocumentKey == key).ToList());
    }

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

    [Fact]
    public void A_version_whose_effective_date_has_not_come_yet_is_not_used()
    {
        var returnsV3 = Version("iade-v3", "iade", "3.0", new DateOnly(2027, 1, 1), DocumentStatus.Active);

        var resolution = Resolve([Hit(returnsV3), Hit(ReturnsV2)], ReturnsV1, ReturnsV2, returnsV3);

        resolution.Kept.Select(hit => hit.Chunk.DocumentId).ShouldBe(["iade-v2"]);
        resolution.Discarded.ShouldHaveSingleItem().Reason.ShouldBe("Yürürlük tarihi (2027-01-01) henüz gelmedi.");
    }

    [Fact]
    public void When_only_an_outdated_version_matched_the_current_version_is_requested_instead()
    {
        var resolution = Resolve([Hit(ReturnsV1)], ReturnsV1, ReturnsV2);

        resolution.Kept.ShouldBeEmpty();
        resolution.Discarded.ShouldHaveSingleItem().Version.ShouldBe(ReturnsV1);
        resolution.NeedsSubstitution.ShouldBe([ReturnsV2]);
    }

    [Fact]
    public void Documents_with_a_single_version_pass_through_untouched()
    {
        var resolution = Resolve([Hit(Shipping)], Shipping);

        resolution.Kept.Select(hit => hit.Chunk.DocumentId).ShouldBe(["kargo"]);
        resolution.Selected.ShouldBeEmpty();
        resolution.Discarded.ShouldBeEmpty();
        resolution.Applied.ShouldBeFalse();
    }

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
