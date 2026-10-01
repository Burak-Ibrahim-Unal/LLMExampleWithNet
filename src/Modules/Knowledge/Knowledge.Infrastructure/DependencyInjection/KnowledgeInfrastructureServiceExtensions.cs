using System.ClientModel;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Options;
using Knowledge.Infrastructure.Embeddings;
using Knowledge.Infrastructure.Ingestion;
using Knowledge.Infrastructure.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Knowledge.Infrastructure.DependencyInjection;

public static class KnowledgeInfrastructureServiceExtensions
{
    public static IServiceCollection AddKnowledgeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KnowledgeBaseOptions>(configuration.GetSection(KnowledgeBaseOptions.SectionName));
        services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.SectionName));
        services.Configure<RetrievalOptions>(configuration.GetSection(RetrievalOptions.SectionName));

        services.AddSingleton<IKnowledgeBaseSource, MarkdownKnowledgeSource>();
        services.AddSingleton<IKnowledgeIndex, KnowledgeIndex>();
        services.AddSingleton(CreateEmbedder);

        return services;
    }

    private static ITextEmbedder CreateEmbedder(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<EmbeddingOptions>>();
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return new DisabledTextEmbedder();
        }

        // Local servers ignore the key, but the OpenAI client requires a non-empty one.
        var client = new OpenAIClient(
            new ApiKeyCredential(string.IsNullOrWhiteSpace(settings.ApiKey) ? "local" : settings.ApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(settings.BaseUrl),
                NetworkTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
            });

        return new OpenAiCompatibleEmbedder(client.GetEmbeddingClient(settings.Model).AsIEmbeddingGenerator(), options);
    }
}
