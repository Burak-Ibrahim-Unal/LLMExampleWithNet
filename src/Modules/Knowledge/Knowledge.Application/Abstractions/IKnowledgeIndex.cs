using Knowledge.Domain.Entities;

namespace Knowledge.Application.Abstractions;

/// <summary>Read model used for retrieval: an in-memory snapshot of every chunk, rebuilt after ingestion.</summary>
public interface IKnowledgeIndex
{
    bool IsReady { get; }

    IndexStatus Status { get; }

    void Rebuild(IReadOnlyList<KnowledgeDocument> documents);

    /// <summary>Tokenizes the query and, in hybrid mode, embeds it once so several searches can reuse it.</summary>
    Task<PreparedQuery> PrepareAsync(string query, CancellationToken cancellationToken = default);

    SearchResult Search(PreparedQuery query, int topK, Func<IndexedChunk, bool>? filter = null);

    /// <summary>Every indexed version of a document family, oldest effective date first.</summary>
    IReadOnlyList<DocumentVersion> GetDocumentVersions(string documentKey);
}

public sealed record DocumentVersion(
    string DocumentId,
    string DocumentKey,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    DocumentStatus Status,
    DocumentCategory Category);

public enum RetrievalMode
{
    /// <summary>BM25 only (no embedding endpoint, or stored vectors unusable).</summary>
    Lexical,

    /// <summary>BM25 and vector similarity fused with Reciprocal Rank Fusion.</summary>
    Hybrid
}

public sealed record IndexStatus(bool IsReady, int DocumentCount, int ChunkCount, RetrievalMode Mode, DateTime? BuiltAtUtc);

public sealed record IndexedChunk(
    Guid ChunkId,
    string DocumentId,
    string DocumentKey,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    DocumentStatus Status,
    DocumentCategory Category,
    string SectionPath,
    string Content);

public sealed record PreparedQuery(string Text, IReadOnlyList<string> Terms, float[]? Vector);

/// <param name="FusedScore">Reciprocal Rank Fusion score used for ordering.</param>
/// <param name="LexicalScore">BM25 score (0 when the chunk shares no term with the query).</param>
/// <param name="LexicalCoverage">Idf-weighted share of the query terms found in the chunk, 0..1.</param>
/// <param name="DenseScore">Cosine similarity, or null in lexical mode.</param>
public sealed record SearchHit(IndexedChunk Chunk, double FusedScore, double LexicalScore, double LexicalCoverage, double? DenseScore);

/// <param name="MaxDenseScore">Best cosine similarity over all candidates (0 in lexical mode).</param>
/// <param name="MaxLexicalCoverage">Best lexical coverage over all candidates.</param>
public sealed record SearchResult(RetrievalMode Mode, IReadOnlyList<SearchHit> Hits, double MaxDenseScore, double MaxLexicalCoverage);
