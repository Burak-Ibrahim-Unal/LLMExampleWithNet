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

/// <summary>
/// Knowledge modülünün altyapı kayıtları: yapılandırma bölümlerini seçenek sınıflarına bağlar ve Application
/// portlarının (<see cref="IKnowledgeBaseSource"/>, <see cref="IKnowledgeIndex"/>, <see cref="ITextEmbedder"/>,
/// <see cref="IGroundedAnswerGenerator"/>) somut adaptörlerini kaydeder. Hangi sağlayıcının kullanılacağına yalnızca
/// burada ve yalnızca yapılandırmaya bakılarak karar verilir; Application katmanı bu kararı hiç görmez.
/// </summary>
public static class KnowledgeInfrastructureServiceExtensions
{
    /// <summary>
    /// Seçenekleri bağlar ve adaptörleri singleton olarak kaydeder; API host'undaki
    /// <c>InfrastructureServiceExtensions</c> tarafından çağrılır.
    /// </summary>
    /// <remarks>
    /// <see cref="KnowledgeIndex"/> singleton olmak zorundadır: bellek içi anlık görüntüyü (snapshot) tüm isteklerle
    /// paylaşır. Embedding ve cevap üreteci fabrika metotlarıyla kaydedilir: hangi adaptörün seçileceği ilk
    /// çözümlemede, test host'larının yaptığı yapılandırma geçersiz kılmaları da uygulanmışken belirlenir; oluşturulan
    /// OpenAI istemcisi her istekte yeniden kurulmaz, uygulama ömrü boyunca kullanılır.
    /// </remarks>
    public static IServiceCollection AddKnowledgeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Application'a ait seçenekler de burada bağlanır: Application yalnızca IOptions<T> tüketir, ayarların hangi
        // yapılandırma kaynağından geldiğini bilmez.
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

    /// <summary>
    /// <see cref="ITextEmbedder"/> uygulamasını yapılandırmaya göre seçer. <c>Embeddings:BaseUrl</c> boşsa Null Object
    /// olan <see cref="DisabledTextEmbedder"/> döner: sistem yalnızca BM25 ile çalışır ve hiçbir çağıranın null kontrolü
    /// yapması gerekmez. Doluysa OpenAI SDK'sının embedding istemcisi Microsoft.Extensions.AI'ın
    /// <c>IEmbeddingGenerator</c> arayüzüne uyarlanır ve <see cref="OpenAiCompatibleEmbedder"/>'a verilir.
    /// </summary>
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

    /// <summary>
    /// <see cref="IGroundedAnswerGenerator"/> uygulamasını yapılandırmaya göre seçer. <c>Llm:BaseUrl</c> boşsa
    /// <see cref="UnconfiguredAnswerGenerator"/> döner: uygulama yine açılır, arama ve doküman uçları çalışır, modele
    /// ulaşması gereken sorular net bir 503 alır. Doluysa (düşünme modu ayarlanmışsa onu taşıyan politikayla) bir
    /// OpenAI istemcisi kurulur, sohbet istemcisi <c>IChatClient</c>'a uyarlanır ve
    /// <see cref="OpenAiCompatibleAnswerGenerator"/>'a verilir.
    /// </summary>
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

    /// <summary>
    /// Embedding ve sohbet için ortak OpenAI istemcisi fabrikası: uç adresi, deneme başına ağ zaman aşımı, yeniden
    /// deneme politikası ve isteğe bağlı <see cref="ChatTemplateKwargsPolicy"/> burada ayarlanır.
    /// </summary>
    /// <remarks>
    /// <c>ClientRetryPolicy(maxRetries: 1)</c>: SDK'nın varsayılan politikası birden çok kez ve giderek uzayan
    /// beklemelerle yeniden dener; kapalı bir sunucuda 503 yanıtı böylece gereksiz yere gecikirdi. Tek yeniden deneme
    /// anlık ağ hatalarına tolerans bırakır ama hatanın hızla görünmesini sağlar. Zaman aşımı her deneme için ayrı
    /// uygulanır. Düşünme politikası yalnızca <paramref name="enableThinking"/> bir değer taşıdığında eklenir
    /// (embedding istemcisi bu parametreyi hiç vermez) ve <c>PipelinePosition.PerCall</c> konumunda olduğu için çağrı
    /// başına bir kez, yeniden deneme politikasından önce çalışır.
    /// </remarks>
    private static OpenAIClient CreateClient(string baseUrl, string apiKey, int timeoutSeconds, bool? enableThinking = null)
    {
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(baseUrl),
            NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            // Yavaş yanıt veren yerel bir modele art arda yeniden gönderilen istekler yardım etmez; yalnızca birbirinin
            // arkasında kuyruğa girerler.
            RetryPolicy = new ClientRetryPolicy(maxRetries: 1)
        };

        if (enableThinking is { } thinking)
        {
            clientOptions.AddPolicy(new ChatTemplateKwargsPolicy(thinking), PipelinePosition.PerCall);
        }

        // Yerel sunucular anahtarı yok sayar ama OpenAI istemcisi boş olmayan bir anahtar ister; boş değer "local" yer
        // tutucusuyla değiştirilir.
        return new OpenAIClient(new ApiKeyCredential(string.IsNullOrWhiteSpace(apiKey) ? "local" : apiKey), clientOptions);
    }
}
