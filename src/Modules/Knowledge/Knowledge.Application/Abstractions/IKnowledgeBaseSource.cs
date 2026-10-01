using Knowledge.Domain.Entities;

namespace Knowledge.Application.Abstractions;

/// <summary>Reads the knowledge base documents (the source of truth) and splits them into sections.</summary>
public interface IKnowledgeBaseSource
{
    Task<IReadOnlyList<SourceDocument>> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed record SourceDocument(
    string SourceId,
    string DocumentKey,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    DocumentStatus Status,
    DocumentCategory Category,
    string? Supersedes,
    string ContentHash,
    IReadOnlyList<SourceSection> Sections);

public sealed record SourceSection(string SectionPath, string Content);
