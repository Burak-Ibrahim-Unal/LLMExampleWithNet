using Knowledge.Application.Abstractions;
using Knowledge.Application.Options;
using Knowledge.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Search;

/// <summary>
/// In-memory hybrid index over every chunk. A rebuild produces an immutable snapshot that replaces the
/// previous one in a single assignment, so concurrent searches never see a half-built index.
/// Brute force is deliberate: the knowledge base holds a few dozen chunks, so a vector database would add
/// operational weight without a measurable gain. <see cref="IKnowledgeIndex"/> keeps that swap possible.
/// </summary>
public sealed class KnowledgeIndex(
    ITextEmbedder embedder,
    IOptions<RetrievalOptions> options,
    ILogger<KnowledgeIndex> logger) : IKnowledgeIndex
{
    private volatile Snapshot? _snapshot;

    public bool IsReady => _snapshot is not null;

    public IndexStatus Status
    {
        get
        {
            var snapshot = _snapshot;

            return snapshot is null
                ? new IndexStatus(false, 0, 0, RetrievalMode.Lexical, null)
                : new IndexStatus(true, snapshot.DocumentCount, snapshot.Chunks.Length, snapshot.Mode, snapshot.BuiltAtUtc);
        }
    }

    public void Rebuild(IReadOnlyList<KnowledgeDocument> documents)
    {
        var entries = documents
            .SelectMany(document => document.Chunks.OrderBy(chunk => chunk.Order).Select(chunk => (Document: document, Chunk: chunk)))
            .ToList();

        var chunks = entries
            .Select(entry => new IndexedChunk(
                entry.Chunk.Id,
                entry.Document.SourceId,
                entry.Document.DocumentKey,
                entry.Document.Title,
                entry.Document.Version,
                entry.Document.EffectiveDate,
                entry.Document.Status,
                entry.Document.Category,
                entry.Chunk.SectionPath,
                entry.Chunk.Content))
            .ToArray();

        // Title and heading words are part of a section's text: "İade Süresi" is often the best match for a question.
        var lexical = new Bm25Index(entries
            .Select(entry => SearchTokenizer.Tokenize($"{entry.Document.Title} {entry.Chunk.SectionPath} {entry.Chunk.Content}"))
            .ToList());

        // Vectors are only comparable with query vectors when the same model produced every one of them.
        var vectorsUsable = embedder.IsEnabled
            && entries.Count > 0
            && entries.All(entry => entry.Chunk.Embedding is not null && entry.Chunk.EmbeddingModel == embedder.ModelName);

        var versions = documents
            .GroupBy(document => document.DocumentKey, StringComparer.Ordinal)
            .ToDictionary(
                family => family.Key,
                family => (IReadOnlyList<DocumentVersion>)family
                    .OrderBy(document => document.EffectiveDate)
                    .Select(document => new DocumentVersion(document.SourceId, document.DocumentKey, document.Title, document.Version, document.EffectiveDate, document.Status, document.Category))
                    .ToList(),
                StringComparer.Ordinal);

        _snapshot = new Snapshot(
            chunks,
            lexical,
            vectorsUsable ? entries.Select(entry => entry.Chunk.Embedding!).ToArray() : null,
            versions,
            documents.Count,
            DateTime.UtcNow);
    }

    public async Task<PreparedQuery> PrepareAsync(string query, CancellationToken cancellationToken = default)
    {
        var terms = SearchTokenizer.Tokenize(query);
        float[]? vector = null;

        if (_snapshot?.Vectors is not null)
        {
            try
            {
                vector = await embedder.EmbedQueryAsync(query, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Query embedding failed; searching with BM25 only.");
            }
        }

        return new PreparedQuery(query, terms, vector);
    }

    public SearchResult Search(PreparedQuery query, int topK, Func<IndexedChunk, bool>? filter = null)
    {
        var snapshot = _snapshot ?? throw new InvalidOperationException("The knowledge index has not been built yet.");
        var settings = options.Value;

        bool IsAllowed(int chunkIndex) => filter is null || filter(snapshot.Chunks[chunkIndex]);

        var lexicalMatches = snapshot.Lexical.Score(query.Terms).Where(match => IsAllowed(match.DocumentIndex)).ToList();
        var lexicalByChunk = lexicalMatches.ToDictionary(match => match.DocumentIndex);
        var rankings = new List<IReadOnlyList<int>>
        {
            lexicalMatches.Take(settings.CandidatePoolSize).Select(match => match.DocumentIndex).ToList()
        };

        Dictionary<int, double>? denseScores = null;

        if (query.Vector is not null && snapshot.Vectors is not null)
        {
            denseScores = Enumerable.Range(0, snapshot.Chunks.Length)
                .Where(IsAllowed)
                .ToDictionary(chunkIndex => chunkIndex, chunkIndex => CosineSimilarity(query.Vector, snapshot.Vectors[chunkIndex]));

            rankings.Add(denseScores
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key)
                .Take(settings.CandidatePoolSize)
                .Select(entry => entry.Key)
                .ToList());
        }

        var hits = RrfFusion.Fuse(rankings, settings.RrfK)
            .Take(topK)
            .Select(fused =>
            {
                lexicalByChunk.TryGetValue(fused.Item, out var lexical);
                return new SearchHit(snapshot.Chunks[fused.Item], fused.Score, lexical.Score, lexical.Coverage, denseScores?[fused.Item]);
            })
            .ToList();

        return new SearchResult(
            denseScores is null ? RetrievalMode.Lexical : RetrievalMode.Hybrid,
            hits,
            MaxDenseScore: denseScores is { Count: > 0 } ? denseScores.Values.Max() : 0,
            MaxLexicalCoverage: lexicalMatches.Count > 0 ? lexicalMatches.Max(match => match.Coverage) : 0);
    }

    public IReadOnlyList<DocumentVersion> GetDocumentVersions(string documentKey)
    {
        return _snapshot?.Versions.GetValueOrDefault(documentKey) ?? [];
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            return 0;
        }

        double dot = 0, leftNorm = 0, rightNorm = 0;

        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        return leftNorm == 0 || rightNorm == 0 ? 0 : dot / Math.Sqrt(leftNorm * rightNorm);
    }

    private sealed record Snapshot(
        IndexedChunk[] Chunks,
        Bm25Index Lexical,
        float[][]? Vectors,
        IReadOnlyDictionary<string, IReadOnlyList<DocumentVersion>> Versions,
        int DocumentCount,
        DateTime BuiltAtUtc)
    {
        public RetrievalMode Mode => Vectors is null ? RetrievalMode.Lexical : RetrievalMode.Hybrid;
    }
}
