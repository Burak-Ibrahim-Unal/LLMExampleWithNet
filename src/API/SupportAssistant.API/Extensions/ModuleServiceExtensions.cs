using Knowledge.Application.Answering;
using Knowledge.Application.BusinessRules;
using Knowledge.Domain.Repositories;
using Knowledge.Infrastructure.Persistence;
using Knowledge.Service;
using Knowledge.Service.Abstractions;

namespace SupportAssistant.API.Extensions;

public static class ModuleServiceExtensions
{
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
