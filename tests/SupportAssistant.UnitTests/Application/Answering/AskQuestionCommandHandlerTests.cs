using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Commands.AskQuestion;
using Knowledge.Application.Contracts;
using Knowledge.Application.Exceptions;
using Knowledge.Application.Options;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Persistence;
using Knowledge.Infrastructure.Search;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Application.Common;
using Shared.Infrastructure.Persistence;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Answering;

/// <summary>The full answering pipeline with the real index and policies; only the language model is faked.</summary>
public sealed class AskQuestionCommandHandlerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeAnswerGenerator _generator = new();
    private readonly KnowledgeIndex _index = new(new FakeTextEmbedder(enabled: false), Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);

    private static KnowledgeDocument Document(string id, string key, string version, DateOnly effectiveDate, DocumentStatus status, DocumentCategory category, params (string Path, string Content)[] sections)
    {
        var document = new KnowledgeDocument(id, key, $"Belge {key}", version, effectiveDate, status, category, null, $"hash-{id}");

        foreach (var section in sections)
        {
            document.AddChunk(section.Path, section.Content);
        }

        return document;
    }

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        _index.Rebuild(
        [
            Document("iade-v1", "iade", "1.0", new DateOnly(2024, 1, 15), DocumentStatus.Superseded, DocumentCategory.Policy,
                ("2. İade Süresi", "Ürünü teslim aldıktan sonra 14 gün içinde iade edebilirsiniz."),
                ("5. İade Kargo Ücreti", "İade kargo ücreti müşteriye aittir.")),
            Document("iade-v2", "iade", "2.0", new DateOnly(2025, 6, 1), DocumentStatus.Active, DocumentCategory.Policy,
                ("2. İade Süresi", "Ürünü teslim aldıktan sonra 30 gün içinde iade edebilirsiniz."),
                ("5. İade Kargo Ücreti", "İade kargosu ücretsizdir.")),
            Document("kargo", "kargo", "1.0", new DateOnly(2025, 3, 1), DocumentStatus.Active, DocumentCategory.Policy,
                ("2. Kargo Ücreti", "750 TL ve üzeri siparişlerde kargo ücretsizdir.")),
            Document("sss", "sss", "1.0", new DateOnly(2024, 2, 1), DocumentStatus.Active, DocumentCategory.Faq,
                ("İade > İade kargo ücretini kim öder?", "İade kargo ücreti müşteriye aittir."))
        ]);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options,
        new EntityConfigurationAssemblyRegistry([Knowledge.Infrastructure.AssemblyReference.Assembly]));

    private async Task<ApiResult<AnswerDto>> AskAsync(string question, IKnowledgeIndex? index = null)
    {
        await using var context = CreateContext();
        var options = Options.Create(new RetrievalOptions());
        var usedIndex = index ?? _index;
        var handler = new AskQuestionCommandHandler(
            usedIndex,
            _generator,
            new KnowledgeBusinessRules(usedIndex),
            new AnswerabilityPolicy(options),
            new VersionResolver(new FixedTimeProvider(new DateOnly(2026, 10, 1))),
            new QuestionLogRepository(context),
            options,
            NullLogger<AskQuestionCommandHandler>.Instance);

        return await handler.Handle(new AskQuestionCommand(question), TestContext.Current.CancellationToken);
    }

    private static string LabelOf(IReadOnlyList<ContextChunk> context, string documentId, string section) =>
        context.Single(source => source.Chunk.DocumentId == documentId && source.Chunk.SectionPath == section).Label;

    [Fact]
    public async Task An_unrelated_question_is_refused_without_calling_the_model()
    {
        var result = await AskAsync("Apple HomeKit ile uyumlu mu?");

        result.StatusCode.ShouldBe(200);
        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.LowRelevance);
        result.Data.Answer.ShouldBe(Messages.Knowledge.NotEnoughInformation);
        result.Data.Sources.ShouldBeEmpty();
        _generator.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Superseded_versions_are_kept_out_of_the_model_context_and_reported()
    {
        var result = await AskAsync("İade süresi kaç gün?");

        _generator.LastContext.ShouldNotBeEmpty();
        _generator.LastContext.ShouldAllBe(source => source.Chunk.DocumentId != "iade-v1");
        _generator.LastContext.ShouldContain(source => source.Chunk.DocumentId == "iade-v2");

        var resolution = result.Data!.VersionResolution;
        resolution.Applied.ShouldBeTrue();
        resolution.Selected.ShouldHaveSingleItem().DocumentId.ShouldBe("iade-v2");
        resolution.Discarded.ShouldHaveSingleItem().DocumentId.ShouldBe("iade-v1");
        resolution.Discarded[0].Reason.ShouldBe("2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.");
    }

    [Fact]
    public async Task Version_resolution_lists_only_document_families_the_answer_is_based_on()
    {
        // "kargo ücreti" also retrieves the return policy's shipping section (v1 and v2), but the answer cites the shipping document.
        _generator.Respond = (_, context) =>
        {
            var shipping = context.First(source => source.Chunk.DocumentId == "kargo");
            return FakeAnswerGenerator.Answer("750 TL ve üzeri siparişlerde kargo ücretsizdir.", new GeneratedCitation(shipping.Label, shipping.Chunk.Content));
        };

        var result = await AskAsync("Kargo ücreti ne kadar?");

        result.Data!.Sources.ShouldHaveSingleItem().DocumentId.ShouldBe("kargo");
        result.Data.VersionResolution.Applied.ShouldBeFalse();
        result.Data.VersionResolution.Discarded.ShouldBeEmpty();
        result.Data.VersionResolution.Selected.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_answer_names_the_cited_document_version_and_section()
    {
        var result = await AskAsync("İade süresi kaç gün?");

        result.Data!.Answerable.ShouldBeTrue();
        result.Data.RefusalReason.ShouldBeEmpty();
        result.Data.Answer.ShouldBe("Ürünü teslim aldıktan sonra 30 gün içinde iade edebilirsiniz.");
        var source = result.Data.Sources.ShouldHaveSingleItem();
        source.DocumentId.ShouldBe("iade-v2");
        source.Version.ShouldBe("2.0");
        source.EffectiveDate.ShouldBe(new DateOnly(2025, 6, 1));
        source.Section.ShouldBe("2. İade Süresi");
        source.QuoteVerified.ShouldBeTrue();
    }

    [Fact]
    public async Task When_the_model_finds_the_sources_insufficient_the_answer_is_an_explicit_refusal()
    {
        // The question clears gate 1 (every word is in the knowledge base); the model is the one that declines.
        _generator.Respond = (_, _) => FakeAnswerGenerator.NotAnswerable("Kaynaklar bu soruyu yanıtlamıyor.");

        var result = await AskAsync("İade süresi kaç gün?");

        _generator.Calls.ShouldBe(1);
        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.ModelInsufficientContext);
        result.Data.MissingInformation.ShouldBe("Kaynaklar bu soruyu yanıtlamıyor.");
        result.Data.Answer.ShouldBe(Messages.Knowledge.NotEnoughInformation);
        result.Data.Sources.ShouldBeEmpty();

        // Version decisions explain the sources of an answer; without an answer there is nothing to explain.
        result.Data.VersionResolution.Applied.ShouldBeFalse();
        result.Data.VersionResolution.Discarded.ShouldBeEmpty();
        result.Data.Diagnostics.Context.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task An_answer_citing_no_provided_source_is_refused()
    {
        _generator.Respond = (_, _) => FakeAnswerGenerator.Answer("30 gün.", new GeneratedCitation("C9", "30 gün"));

        var result = await AskAsync("İade süresi kaç gün?");

        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.NoValidCitations);
        result.Data.Sources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("iade-v2", "5. İade Kargo Ücreti", "sss", "İade > İade kargo ücretini kim öder?", true)]
    [InlineData("sss", "İade > İade kargo ücretini kim öder?", "iade-v2", "5. İade Kargo Ücreti", false)]
    public async Task Conflicts_reported_by_the_model_are_checked_against_the_precedence_rule(
        string chosenDocument, string chosenSection, string rejectedDocument, string rejectedSection, bool ruleSatisfied)
    {
        _generator.Respond = (_, context) =>
        {
            var chosen = LabelOf(context, chosenDocument, chosenSection);
            var rejected = LabelOf(context, rejectedDocument, rejectedSection);
            return FakeAnswerGenerator.Answer("İade kargosu ücretsizdir.", new GeneratedCitation(chosen, "kargo")) with
            {
                Conflicts = [new GeneratedConflict("İade kargo ücreti", chosen, [rejected], "Politika daha yeni ve SSS'den önceliklidir.")]
            };
        };

        var result = await AskAsync("İade kargo ücretini kim öder?");

        var conflict = result.Data!.Conflicts.ShouldHaveSingleItem();
        conflict.Chosen.DocumentId.ShouldBe(chosenDocument);
        conflict.Rejected.ShouldHaveSingleItem().DocumentId.ShouldBe(rejectedDocument);
        conflict.RuleSatisfied.ShouldBe(ruleSatisfied);
    }

    [Theory]
    [InlineData(AnswerGenerationFailure.Unavailable, 503, Messages.Knowledge.LlmUnavailable)]
    [InlineData(AnswerGenerationFailure.InvalidOutput, 502, Messages.Knowledge.LlmInvalidOutput)]
    public async Task Model_failures_are_reported_with_their_own_status(AnswerGenerationFailure failure, int statusCode, string message)
    {
        _generator.Failure = new AnswerGenerationException(failure, "model error");

        var result = await AskAsync("İade süresi kaç gün?");

        result.Success.ShouldBeFalse();
        result.StatusCode.ShouldBe(statusCode);
        result.Message.ShouldBe(message);
    }

    [Fact]
    public async Task Every_answered_question_is_logged()
    {
        await AskAsync("İade süresi kaç gün?");

        await using var context = CreateContext();
        var log = await context.Set<Knowledge.Domain.Entities.QuestionLog>().SingleAsync(TestContext.Current.CancellationToken);
        log.Question.ShouldBe("İade süresi kaç gün?");
        log.Answerable.ShouldBeTrue();
        log.ResponseJson.ShouldContain("iade-v2");
    }

    [Theory]
    [InlineData("   ", "Soru boş olamaz.")]
    [InlineData("çok uzun", "Soru en fazla 500 karakter olabilir.")]
    public async Task Invalid_questions_are_rejected(string question, string message)
    {
        var result = await AskAsync(question == "çok uzun" ? new string('a', 501) : question);

        result.StatusCode.ShouldBe(400);
        result.Message.ShouldBe(message);
    }

    [Fact]
    public async Task Questions_wait_for_the_index()
    {
        var emptyIndex = new KnowledgeIndex(new FakeTextEmbedder(enabled: false), Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);

        var result = await AskAsync("İade süresi kaç gün?", emptyIndex);

        result.StatusCode.ShouldBe(503);
    }
}
