using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;

namespace SupportAssistant.UnitTests.TestDoubles;

internal sealed class StubKnowledgeBaseSource : IKnowledgeBaseSource
{
    public IReadOnlyList<SourceDocument> Documents { get; set; } = [];

    public Exception? Failure { get; set; }

    public Task<IReadOnlyList<SourceDocument>> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Failure is not null ? Task.FromException<IReadOnlyList<SourceDocument>>(Failure) : Task.FromResult(Documents);
    }

    public static SourceDocument Document(string sourceId, string contentHash, params SourceSection[] sections)
    {
        return new SourceDocument(
            sourceId,
            DocumentKey: sourceId,
            Title: $"Başlık {sourceId}",
            Version: "1.0",
            EffectiveDate: new DateOnly(2025, 1, 1),
            DocumentStatus.Active,
            DocumentCategory.Policy,
            Supersedes: null,
            contentHash,
            sections);
    }
}
