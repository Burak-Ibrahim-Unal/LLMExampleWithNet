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
        services.AddScoped<IKnowledgeDocumentRepository, KnowledgeDocumentRepository>();
        services.AddScoped<KnowledgeBusinessRules>();
        services.AddScoped<IKnowledgeService, KnowledgeService>();

        return services;
    }
}
