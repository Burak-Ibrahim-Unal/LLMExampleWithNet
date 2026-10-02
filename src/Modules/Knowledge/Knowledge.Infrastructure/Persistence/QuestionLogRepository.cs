using Knowledge.Domain.Entities;
using Knowledge.Domain.Repositories;
using Shared.Infrastructure.Persistence;

namespace Knowledge.Infrastructure.Persistence;

/// <summary>
/// <see cref="IQuestionLogRepository"/> portunun EF Core uygulaması. Denetim kaydı yalnızca eklenip kaydedildiği için
/// genel <c>EfRepository</c> davranışı yeterlidir; ek sorgu yoktur.
/// </summary>
/// <remarks>
/// Ayrı bir tip olarak var olmasının nedeni bağımlılık yönüdür: <c>AskQuestionCommandHandler</c> domain'deki arayüze
/// bağlıdır ve EF Core'u hiç görmez; somut EF uygulaması yalnızca DI kaydında (scoped) ve veritabanıyla çalışan
/// testlerde seçilir.
/// </remarks>
/// <param name="dbContext">İstek kapsamındaki paylaşılan veritabanı bağlamı.</param>
public sealed class QuestionLogRepository(AppDbContext dbContext) : EfRepository<QuestionLog>(dbContext), IQuestionLogRepository;
