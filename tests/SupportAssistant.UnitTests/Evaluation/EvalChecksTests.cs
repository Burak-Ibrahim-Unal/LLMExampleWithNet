using Knowledge.Application.Contracts;
using Shouldly;
using SupportAssistant.Eval;

namespace SupportAssistant.UnitTests.Evaluation;

public sealed class EvalChecksTests
{
    private static AnswerDto Answer(string text, bool answerable = true, string[]? sources = null, string[]? discarded = null) => new(
        "soru",
        answerable,
        text,
        (sources ?? []).Select(id => new AnswerSourceDto(id, "Başlık", "1.0", new DateOnly(2025, 1, 1), "active", "politika", "Bölüm", "alıntı", true)).ToList(),
        new VersionResolutionDto(
            (discarded ?? []).Length > 0,
            "kural",
            [],
            (discarded ?? []).Select(id => new DiscardedVersionDto(id, "Başlık", "1.0", new DateOnly(2024, 1, 1), "gerekçe")).ToList()),
        [],
        string.Empty,
        answerable ? string.Empty : "LowRelevance",
        new AnswerDiagnosticsDto("hybrid", 0.7, 1.0, [], [], "model", 100, null, null));

    [Theory]
    [InlineData("Kargo ücretsizdir.", "ücretsiz", true)]
    [InlineData("Kargo ucretsizdir.", "ÜCRETSİZ", true)]
    [InlineData("Ürünü 300 gün içinde iade edin.", "30 gün", false)]
    [InlineData("Kargo ücreti alınır.", "ücretsiz", false)]
    [InlineData("Yalnızca 2.4 GHz ağlar desteklenir.", "2,4 GHz", true)]
    public void Phrases_match_at_word_starts_ignoring_case_punctuation_and_turkish_characters(string text, string phrase, bool expected)
    {
        EvalChecks.ContainsPhrase(text, phrase).ShouldBe(expected);
    }

    [Fact]
    public void Every_must_contain_group_needs_one_of_its_phrases()
    {
        var expect = new EvalExpectation(true, MustContain: [["30 gün"], ["45 gün", "otuz gün"]]);

        var checks = EvalChecks.Evaluate(expect, Answer("Ürünü 30 gün içinde iade edebilirsiniz."));

        checks.Where(check => check.Name == "içerik").Select(check => check.Passed).ShouldBe([true, false]);
    }

    [Fact]
    public void Forbidden_phrases_and_forbidden_sources_fail_their_checks()
    {
        var expect = new EvalExpectation(true, SourcesAnyOf: ["iade-v2"], ForbiddenSources: ["iade-v1"], MustNotContain: ["14 gün"]);

        var checks = EvalChecks.Evaluate(expect, Answer("İade süresi 14 gündür.", sources: ["iade-v1", "iade-v2"]));

        checks.Single(check => check.Name == "kaynak").Passed.ShouldBeTrue();
        checks.Single(check => check.Name == "yasak kaynak yok").Passed.ShouldBeFalse();
        checks.Single(check => check.Name == "yasak ifade").Passed.ShouldBeFalse();
    }

    [Fact]
    public void All_of_sources_must_each_be_cited()
    {
        var expect = new EvalExpectation(true, SourcesAllOf: ["kargo", "kurulum"]);

        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["kargo"])).Single(check => check.Name == "kaynaklar").Passed.ShouldBeFalse();
        EvalChecks.Evaluate(expect, Answer("Yanıt.", sources: ["kurulum", "kargo"])).Single(check => check.Name == "kaynaklar").Passed.ShouldBeTrue();
    }

    [Fact]
    public void Outdated_versions_must_be_reported_as_discarded()
    {
        var expect = new EvalExpectation(true, DiscardedVersions: ["iade-v1"]);

        EvalChecks.Evaluate(expect, Answer("30 gün.", discarded: [])).Single(check => check.Name == "eski sürüm elendi").Passed.ShouldBeFalse();
        EvalChecks.Evaluate(expect, Answer("30 gün.", discarded: ["iade-v1"])).Single(check => check.Name == "eski sürüm elendi").Passed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void A_question_that_must_be_declined_is_only_checked_for_the_refusal(bool actuallyAnswered, bool expectedPass)
    {
        var expect = new EvalExpectation(false, MustContain: [["olmayacak ifade"]]);

        var checks = EvalChecks.Evaluate(expect, Answer("Bilgi bulunamadı.", answerable: actuallyAnswered));

        checks.ShouldHaveSingleItem().Name.ShouldBe("yanıtlanabilirlik");
        checks[0].Passed.ShouldBe(expectedPass);
    }

    [Fact]
    public void Retrieval_hit_needs_one_expected_source_or_all_of_them()
    {
        EvalChecks.RetrievalHit(new EvalExpectation(true, SourcesAnyOf: ["a", "b"]), ["x", "b"]).ShouldBe(true);
        EvalChecks.RetrievalHit(new EvalExpectation(true, SourcesAllOf: ["a", "b"]), ["a", "x"]).ShouldBe(false);
        EvalChecks.RetrievalHit(new EvalExpectation(false), ["a"]).ShouldBeNull();
    }
}
