using Shared.Kernel.Abstractions;

namespace Knowledge.Domain.Entities;

/// <summary>
/// One version of a knowledge base document. Versions of the same procedure share a <see cref="DocumentKey"/>
/// (e.g. "iade-politikasi") and differ in <see cref="Version"/>, <see cref="EffectiveDate"/> and <see cref="Status"/>.
/// </summary>
public sealed class KnowledgeDocument : EntityBase
{
    private readonly List<DocumentChunk> _chunks = [];

    private KnowledgeDocument()
    {
    }

    public KnowledgeDocument(
        string sourceId,
        string documentKey,
        string title,
        string version,
        DateOnly effectiveDate,
        DocumentStatus status,
        DocumentCategory category,
        string? supersedes,
        string contentHash)
    {
        SourceId = sourceId;
        Revise(documentKey, title, version, effectiveDate, status, category, supersedes, contentHash);
    }

    /// <summary>Stable identifier from the file's front matter, e.g. "iade-politikasi-v2".</summary>
    public string SourceId { get; private set; } = string.Empty;

    public string DocumentKey { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    public DateOnly EffectiveDate { get; private set; }

    public DocumentStatus Status { get; private set; }

    public DocumentCategory Category { get; private set; }

    public string? Supersedes { get; private set; }

    /// <summary>Hash of the source file; an unchanged hash means stored chunks and embeddings are still valid.</summary>
    public string ContentHash { get; private set; } = string.Empty;

    public IReadOnlyList<DocumentChunk> Chunks => _chunks;

    public void Revise(
        string documentKey,
        string title,
        string version,
        DateOnly effectiveDate,
        DocumentStatus status,
        DocumentCategory category,
        string? supersedes,
        string contentHash)
    {
        DocumentKey = documentKey;
        Title = title;
        Version = version;
        EffectiveDate = effectiveDate;
        Status = status;
        Category = category;
        Supersedes = supersedes;
        ContentHash = contentHash;
    }

    public DocumentChunk AddChunk(string sectionPath, string content)
    {
        var chunk = new DocumentChunk(Id, _chunks.Count, sectionPath, content);
        _chunks.Add(chunk);
        return chunk;
    }

    public void ClearChunks()
    {
        _chunks.Clear();
    }
}
