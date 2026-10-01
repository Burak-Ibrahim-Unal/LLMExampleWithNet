using Knowledge.Domain.Entities;
using Knowledge.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace Knowledge.Infrastructure.Persistence;

public sealed class KnowledgeDocumentRepository : EfRepository<KnowledgeDocument>, IKnowledgeDocumentRepository
{
    private readonly AppDbContext _dbContext;

    public KnowledgeDocumentRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<KnowledgeDocument>> ListWithChunksAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<KnowledgeDocument>()
            .Include(document => document.Chunks)
            .OrderBy(document => document.SourceId)
            .ToListAsync(cancellationToken);
    }

    public Task<KnowledgeDocument?> GetBySourceIdWithChunksAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<KnowledgeDocument>()
            .Include(document => document.Chunks)
            .FirstOrDefaultAsync(document => document.SourceId == sourceId, cancellationToken);
    }
}
