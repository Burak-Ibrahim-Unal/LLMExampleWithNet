using Knowledge.Domain.Entities;
using Shared.Kernel.Abstractions;

namespace Knowledge.Domain.Repositories;

public interface IKnowledgeDocumentRepository : IRepository<KnowledgeDocument>
{
    Task<List<KnowledgeDocument>> ListWithChunksAsync(CancellationToken cancellationToken = default);

    Task<KnowledgeDocument?> GetBySourceIdWithChunksAsync(string sourceId, CancellationToken cancellationToken = default);
}
