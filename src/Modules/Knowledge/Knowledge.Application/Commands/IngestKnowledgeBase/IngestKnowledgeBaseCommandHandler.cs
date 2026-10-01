using Knowledge.Application.Abstractions;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Application.Exceptions;
using Knowledge.Domain.Entities;
using Knowledge.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Application.Common;

namespace Knowledge.Application.Commands.IngestKnowledgeBase;

/// <summary>
/// Reconciles stored documents with the knowledge base files: unchanged files keep their chunks and embeddings,
/// changed files are re-chunked and re-embedded, deleted files are removed. The search index is rebuilt afterwards.
/// </summary>
public sealed class IngestKnowledgeBaseCommandHandler(
    IKnowledgeBaseSource source,
    IKnowledgeDocumentRepository repository,
    ITextEmbedder embedder,
    IKnowledgeIndex index,
    KnowledgeBusinessRules rules,
    ILogger<IngestKnowledgeBaseCommandHandler> logger) : IRequestHandler<IngestKnowledgeBaseCommand, ApiResult<IngestionSummaryDto>>
{
    public async Task<ApiResult<IngestionSummaryDto>> Handle(IngestKnowledgeBaseCommand request, CancellationToken cancellationToken)
    {
        IReadOnlyList<SourceDocument> sources;

        try
        {
            sources = await source.LoadAsync(cancellationToken);
        }
        catch (KnowledgeBaseFormatException exception)
        {
            return ApiResult<IngestionSummaryDto>.Fail(exception.Message, 422);
        }

        var emptyError = rules.CheckKnowledgeBaseNotEmpty<IngestionSummaryDto>(sources.Count);
        if (emptyError is not null)
        {
            return emptyError;
        }

        var duplicateError = rules.CheckUniqueDocumentIds<IngestionSummaryDto>(sources.Select(document => document.SourceId));
        if (duplicateError is not null)
        {
            return duplicateError;
        }

        var stored = (await repository.ListWithChunksAsync(cancellationToken))
            .ToDictionary(document => document.SourceId, StringComparer.OrdinalIgnoreCase);
        var current = new List<KnowledgeDocument>(sources.Count);
        int added = 0, updated = 0, unchanged = 0;

        foreach (var document in sources)
        {
            if (stored.Remove(document.SourceId, out var existing))
            {
                current.Add(existing);

                if (existing.ContentHash == document.ContentHash)
                {
                    unchanged++;
                    continue;
                }

                existing.Revise(document.DocumentKey, document.Title, document.Version, document.EffectiveDate, document.Status, document.Category, document.Supersedes, document.ContentHash);
                existing.ClearChunks();
                AddSections(existing, document);
                updated++;
                continue;
            }

            var created = new KnowledgeDocument(document.SourceId, document.DocumentKey, document.Title, document.Version, document.EffectiveDate, document.Status, document.Category, document.Supersedes, document.ContentHash);
            AddSections(created, document);
            await repository.AddAsync(created, cancellationToken);
            current.Add(created);
            added++;
        }

        foreach (var removed in stored.Values)
        {
            repository.Remove(removed);
        }

        var (embeddedChunks, warning) = await EmbedMissingChunksAsync(current, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        index.Rebuild(current);
        var status = index.Status;

        var summary = new IngestionSummaryDto(
            Documents: current.Count,
            Chunks: status.ChunkCount,
            Added: added,
            Updated: updated,
            Removed: stored.Count,
            Unchanged: unchanged,
            EmbeddedChunks: embeddedChunks,
            RetrievalMode: status.Mode == RetrievalMode.Hybrid ? "hybrid" : "lexical",
            Warning: warning);

        return ApiResult<IngestionSummaryDto>.Ok(summary, Messages.Knowledge.Reindexed);
    }

    private static void AddSections(KnowledgeDocument document, SourceDocument source)
    {
        foreach (var section in source.Sections)
        {
            document.AddChunk(section.SectionPath, section.Content);
        }
    }

    /// <summary>
    /// Embeds chunks that have no vector from the configured model. An unreachable embedding server does not fail
    /// ingestion: the index then runs in BM25-only mode and a later reindex fills the vectors in.
    /// </summary>
    private async Task<(int EmbeddedChunks, string? Warning)> EmbedMissingChunksAsync(IReadOnlyList<KnowledgeDocument> documents, CancellationToken cancellationToken)
    {
        if (!embedder.IsEnabled)
        {
            return (0, null);
        }

        var pending = documents
            .SelectMany(document => document.Chunks.Select(chunk => (Document: document, Chunk: chunk)))
            .Where(entry => entry.Chunk.Embedding is null || entry.Chunk.EmbeddingModel != embedder.ModelName)
            .ToList();

        if (pending.Count == 0)
        {
            return (0, null);
        }

        try
        {
            var vectors = await embedder.EmbedDocumentsAsync(
                pending.Select(entry => new DocumentEmbeddingInput(entry.Document.Title, EmbeddingText(entry.Document, entry.Chunk))).ToList(),
                cancellationToken);

            for (var i = 0; i < pending.Count; i++)
            {
                pending[i].Chunk.SetEmbedding(vectors[i], embedder.ModelName);
            }

            return (pending.Count, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Embedding {ChunkCount} chunks failed; the index will use BM25 only.", pending.Count);
            return (0, Messages.Knowledge.EmbeddingUnavailable);
        }
    }

    // The title and heading give a short section ("2. İade Süresi") the context it needs to be found.
    private static string EmbeddingText(KnowledgeDocument document, DocumentChunk chunk) =>
        $"{document.Title} > {chunk.SectionPath}\n{chunk.Content}";
}
