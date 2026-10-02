using Knowledge.Application.Answering;
using Knowledge.Application.BusinessRules;
using Knowledge.Domain.Repositories;
using Knowledge.Infrastructure.Persistence;
using Knowledge.Service;
using Knowledge.Service.Abstractions;

namespace SupportAssistant.API.Extensions;

/// <summary>
/// Knowledge modülünün uygulama servislerinin DI kayıtları: zaman kaynağı, repository'ler, iş kuralları, cevaplama
/// politikaları ve <c>IKnowledgeService</c> cephesi.
/// </summary>
public static class ModuleServiceExtensions
{
    /// <summary>
    /// Modül servislerini uygun yaşam süreleriyle kaydeder.
    /// </summary>
    /// <remarks>
    /// <c>TimeProvider.System</c> singleton olarak enjekte edilir: <c>VersionResolver</c> "bugün" bilgisini buradan alır,
    /// testler ise sabit tarih veren bir sağlayıcı kullanarak sürüm seçimini deterministik biçimde sınar. Repository'ler
    /// scoped <c>AppDbContext</c>'e bağlı olduğu için scoped'dır; <c>IKnowledgeService</c> de handler'ları istek scope'u
    /// içinden çözdüğü için scoped'dır (singleton olsaydı istek scope'una ait DbContext'i yakalardı). Durumsuz
    /// <c>AnswerabilityPolicy</c> ve <c>VersionResolver</c> güvenle paylaşılabildiği için singleton'dır;
    /// <c>KnowledgeBusinessRules</c> istek başına oluşturulur.
    /// </remarks>
    public static IServiceCollection AddModuleServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IKnowledgeDocumentRepository, KnowledgeDocumentRepository>();
        services.AddScoped<IQuestionLogRepository, QuestionLogRepository>();
        services.AddScoped<KnowledgeBusinessRules>();
        services.AddSingleton<AnswerabilityPolicy>();
        services.AddSingleton<VersionResolver>();
        services.AddScoped<IKnowledgeService, KnowledgeService>();

        return services;
    }
}
