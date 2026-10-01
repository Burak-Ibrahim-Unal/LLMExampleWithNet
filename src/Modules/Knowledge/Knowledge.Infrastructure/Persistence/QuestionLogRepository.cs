using Knowledge.Domain.Entities;
using Knowledge.Domain.Repositories;
using Shared.Infrastructure.Persistence;

namespace Knowledge.Infrastructure.Persistence;

public sealed class QuestionLogRepository(AppDbContext dbContext) : EfRepository<QuestionLog>(dbContext), IQuestionLogRepository;
