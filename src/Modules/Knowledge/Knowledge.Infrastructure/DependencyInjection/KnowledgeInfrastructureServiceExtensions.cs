using System.ClientModel;
using System.ClientModel.Primitives;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Options;
using Knowledge.Infrastructure.Embeddings;
using Knowledge.Infrastructure.Ingestion;
using Knowledge.Infrastructure.Llm;
using Knowledge.Infrastructure.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));

        services.AddSingleton<IKnowledgeBaseSource, MarkdownKnowledgeSource>();
        services.AddSingleton<IKnowledgeIndex, KnowledgeIndex>();
        services.AddSingleton(CreateEmbedder);
        services.AddSingleton(CreateAnswerGenerator);

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

        var client = CreateClient(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        return new OpenAiCompatibleEmbedder(client.GetEmbeddingClient(settings.Model).AsIEmbeddingGenerator(), options);
    }

    private static IGroundedAnswerGenerator CreateAnswerGenerator(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<LlmOptions>>();
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return new UnconfiguredAnswerGenerator();
        }

        var client = CreateClient(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds, settings.EnableThinking);

        return new OpenAiCompatibleAnswerGenerator(
            client.GetChatClient(settings.ChatModel).AsIChatClient(),
            options,
            serviceProvider.GetRequiredService<ILogger<OpenAiCompatibleAnswerGenerator>>());
    }

    private static OpenAIClient CreateClient(string baseUrl, string apiKey, int timeoutSeconds, bool? enableThinking = null)
    {
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(baseUrl),
            NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            // A local model answering slowly is not helped by repeated requests queuing behind each other.
            RetryPolicy = new ClientRetryPolicy(maxRetries: 1)
        };

        if (enableThinking is { } thinking)
        {
            clientOptions.AddPolicy(new ChatTemplateKwargsPolicy(thinking), PipelinePosition.PerCall);
        }

        // Local servers ignore the key, but the OpenAI client requires a non-empty one.
        return new OpenAIClient(new ApiKeyCredential(string.IsNullOrWhiteSpace(apiKey) ? "local" : apiKey), clientOptions);
    }
}
