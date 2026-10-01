using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Application.Exceptions;
using Knowledge.Application.Options;
using Knowledge.Domain.Entities;
using Knowledge.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Application.Common;

namespace Knowledge.Application.Commands.AskQuestion;

/// <summary>
/// Retrieval → gate 1 (evidence) → version resolution → language model → gates 2–3 (model judgement, citations).
/// Every gate can withhold the answer; a withheld answer is an explicit "not enough information" response.
/// </summary>
public sealed class AskQuestionCommandHandler(
    IKnowledgeIndex index,
    IGroundedAnswerGenerator generator,
    KnowledgeBusinessRules rules,
    AnswerabilityPolicy answerabilityPolicy,
    VersionResolver versionResolver,
    IQuestionLogRepository questionLogs,
    IOptions<RetrievalOptions> options,
    ILogger<AskQuestionCommandHandler> logger) : IRequestHandler<AskQuestionCommand, ApiResult<AnswerDto>>
{
    // Sections fetched from the version in effect when only an outdated version matched the question.
    private const int SubstituteSectionCount = 2;

    private static readonly JsonSerializerOptions LogJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<ApiResult<AnswerDto>> Handle(AskQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = request.Question?.Trim() ?? string.Empty;

        var requiredError = rules.CheckQuestionRequired<AnswerDto>(question);
        if (requiredError is not null)
        {
            return requiredError;
        }

        var lengthError = rules.CheckQuestionLength<AnswerDto>(question);
        if (lengthError is not null)
        {
            return lengthError;
        }

        var readyError = rules.CheckIndexReady<AnswerDto>();
        if (readyError is not null)
        {
            return readyError;
        }

        var stopwatch = Stopwatch.StartNew();
        var settings = options.Value;

        // Retrieve more than the model will see: outdated versions are removed in the next step.
        var prepared = await index.PrepareAsync(question, cancellationToken);
        var retrieval = index.Search(prepared, settings.TopK * 2);
        var candidateDocumentIds = retrieval.Hits.Select(hit => hit.Chunk.DocumentId).Distinct().ToList();

        if (!answerabilityPolicy.HasEnoughEvidence(retrieval))
        {
            return await RefuseAsync(question, RefusalReasons.LowRelevance, string.Empty, Diagnostics(retrieval, candidateDocumentIds, [], string.Empty, stopwatch, null), cancellationToken);
        }

        var resolution = versionResolver.Resolve(retrieval.Hits, index.GetDocumentVersions);
        var context = WithSubstitutes(retrieval.Hits, resolution, prepared)
            .Take(settings.TopK)
            .Select((hit, position) => new ContextChunk($"C{position + 1}", hit.Chunk))
            .ToList();

        // Everything relevant was superseded or not yet in effect: there is nothing the model may use.
        if (context.Count == 0)
        {
            return await RefuseAsync(question, RefusalReasons.NoSourceInEffect, string.Empty, Diagnostics(retrieval, candidateDocumentIds, context, string.Empty, stopwatch, null), cancellationToken);
        }

        GeneratedAnswer generated;

        try
        {
            generated = await generator.GenerateAsync(question, context, cancellationToken);
        }
        catch (AnswerGenerationException exception)
        {
            logger.LogWarning(exception, "Answer generation failed ({Failure}).", exception.Failure);

            return exception.Failure == AnswerGenerationFailure.Unavailable
                ? ApiResult<AnswerDto>.Fail(Messages.Knowledge.LlmUnavailable, 503)
                : ApiResult<AnswerDto>.Fail(Messages.Knowledge.LlmInvalidOutput, 502);
        }

        var diagnostics = Diagnostics(retrieval, candidateDocumentIds, context, generated.Model, stopwatch, generated);

        if (!generated.Answerable)
        {
            return await RefuseAsync(question, RefusalReasons.ModelInsufficientContext, generated.MissingInformation, diagnostics, cancellationToken);
        }

        var citations = CitationValidator.Validate(generated.Citations, context);

        if (citations.Count == 0)
        {
            return await RefuseAsync(question, RefusalReasons.NoValidCitations, generated.MissingInformation, diagnostics, cancellationToken);
        }

        var answerText = AnswerText.Clean(generated.Answer, citations.Select(citation => citation.Quote).ToList());

        // If the model's text consisted only of markers or quotes, the cited text itself is the answer.
        if (string.IsNullOrWhiteSpace(answerText))
        {
            answerText = string.Join(" ", citations.Select(citation => citation.Quote).Where(quote => quote.Length > 0).Distinct());
        }

        if (string.IsNullOrWhiteSpace(answerText))
        {
            return await RefuseAsync(question, RefusalReasons.NoValidCitations, generated.MissingInformation, diagnostics, cancellationToken);
        }

        // Only the document families the answer actually rests on are reported as version decisions.
        var citedFamilies = citations.Select(citation => citation.Source.Chunk.DocumentKey).ToHashSet(StringComparer.Ordinal);

        var answer = new AnswerDto(
            question,
            Answerable: true,
            Answer: answerText,
            Sources: citations.Select(ToSourceDto).ToList(),
            VersionResolution: ToDto(resolution, citedFamilies),
            Conflicts: CheckConflicts(generated.Conflicts, context),
            MissingInformation: generated.MissingInformation.Trim(),
            RefusalReason: string.Empty,
            Diagnostics: diagnostics);

        await LogAsync(answer, cancellationToken);
        return ApiResult<AnswerDto>.Ok(answer);
    }

    /// <summary>
    /// An explicit "not enough information" response. It carries no version decisions — those explain the sources of
    /// an answer — while diagnostics still show what was retrieved and shown to the model.
    /// </summary>
    private async Task<ApiResult<AnswerDto>> RefuseAsync(
        string question,
        string reason,
        string missingInformation,
        AnswerDiagnosticsDto diagnostics,
        CancellationToken cancellationToken)
    {
        var refusal = new AnswerDto(
            question,
            Answerable: false,
            Answer: Messages.Knowledge.NotEnoughInformation,
            Sources: [],
            VersionResolution: new VersionResolutionDto(false, VersionResolver.Rule, [], []),
            Conflicts: [],
            MissingInformation: missingInformation.Trim(),
            RefusalReason: reason,
            Diagnostics: diagnostics);

        await LogAsync(refusal, cancellationToken);
        return ApiResult<AnswerDto>.Ok(refusal, Messages.Knowledge.NotEnoughInformation);
    }

    /// <summary>
    /// Keeps the retrieval order; where an outdated version had matched and the version in effect had not,
    /// the best sections of the version in effect take the outdated sections' place.
    /// </summary>
    private List<SearchHit> WithSubstitutes(IReadOnlyList<SearchHit> candidates, VersionResolution resolution, PreparedQuery query)
    {
        var substitutes = resolution.NeedsSubstitution.ToDictionary(
            version => version.DocumentKey,
            version => index.Search(query, SubstituteSectionCount, chunk => chunk.DocumentId == version.DocumentId).Hits);

        var kept = resolution.Kept.Select(hit => hit.Chunk.ChunkId).ToHashSet();
        var merged = new List<SearchHit>();

        foreach (var hit in candidates)
        {
            if (kept.Contains(hit.Chunk.ChunkId))
            {
                merged.Add(hit);
            }
            else if (substitutes.Remove(hit.Chunk.DocumentKey, out var replacement))
            {
                merged.AddRange(replacement);
            }
        }

        return merged;
    }

    private static IReadOnlyList<ConflictDto> CheckConflicts(IReadOnlyList<GeneratedConflict> conflicts, IReadOnlyList<ContextChunk> context)
    {
        var sourcesByLabel = context.ToDictionary(source => source.Label, StringComparer.OrdinalIgnoreCase);
        var checkedConflicts = new List<ConflictDto>();

        foreach (var conflict in conflicts)
        {
            if (!sourcesByLabel.TryGetValue(SourceLabel.Normalize(conflict.ChosenChunkLabel), out var chosen))
            {
                continue;
            }

            var rejected = conflict.RejectedChunkLabels
                .Select(SourceLabel.Normalize)
                .Distinct()
                .Where(label => label != chosen.Label && sourcesByLabel.ContainsKey(label))
                .Select(label => sourcesByLabel[label])
                .ToList();

            if (rejected.Count == 0)
            {
                continue;
            }

            checkedConflicts.Add(new ConflictDto(
                conflict.Topic.Trim(),
                ToConflictSource(chosen),
                rejected.Select(ToConflictSource).ToList(),
                conflict.Reason.Trim(),
                RuleSatisfied: rejected.All(other => SourcePrecedence.Outranks(chosen.Chunk, other.Chunk))));
        }

        return checkedConflicts;
    }

    private async Task LogAsync(AnswerDto answer, CancellationToken cancellationToken)
    {
        var log = new QuestionLog(
            answer.Question,
            answer.Answerable,
            answer.RefusalReason,
            answer.Diagnostics.Model,
            answer.Diagnostics.LatencyMs,
            JsonSerializer.Serialize(answer, LogJsonOptions));

        await questionLogs.AddAsync(log, cancellationToken);
        await questionLogs.SaveChangesAsync(cancellationToken);
    }

    private static VersionResolutionDto ToDto(VersionResolution resolution, IReadOnlySet<string> relevantFamilies)
    {
        var discarded = resolution.Discarded
            .Where(item => relevantFamilies.Contains(item.Version.DocumentKey))
            .Select(item => new DiscardedVersionDto(item.Version.DocumentId, item.Version.Title, item.Version.Version, item.Version.EffectiveDate, item.Reason))
            .ToList();

        var selected = resolution.Selected
            .Where(version => relevantFamilies.Contains(version.DocumentKey))
            .Select(version => new VersionRefDto(version.DocumentId, version.Title, version.Version, version.EffectiveDate))
            .ToList();

        return new VersionResolutionDto(discarded.Count > 0, VersionResolver.Rule, selected, discarded);
    }

    private static AnswerSourceDto ToSourceDto(ValidatedCitation citation)
    {
        var chunk = citation.Source.Chunk;
        return new AnswerSourceDto(
            chunk.DocumentId,
            chunk.Title,
            chunk.Version,
            chunk.EffectiveDate,
            chunk.Status.ToApi(),
            chunk.Category.ToApi(),
            chunk.SectionPath,
            citation.Quote,
            citation.QuoteVerified);
    }

    private static ConflictSourceDto ToConflictSource(ContextChunk source) =>
        new(source.Chunk.DocumentId, source.Chunk.Version, source.Chunk.EffectiveDate, source.Chunk.Category.ToApi(), source.Chunk.SectionPath);

    private static AnswerDiagnosticsDto Diagnostics(
        SearchResult retrieval,
        IReadOnlyList<string> candidateDocumentIds,
        IReadOnlyList<ContextChunk> context,
        string model,
        Stopwatch stopwatch,
        GeneratedAnswer? generated) => new(
        retrieval.Mode.ToApi(),
        retrieval.MaxDenseScore,
        retrieval.MaxLexicalCoverage,
        candidateDocumentIds,
        context.Select(source => new ContextSourceDto(source.Label, source.Chunk.DocumentId, source.Chunk.Version, source.Chunk.SectionPath)).ToList(),
        model,
        stopwatch.ElapsedMilliseconds,
        generated?.InputTokens,
        generated?.OutputTokens);
}
