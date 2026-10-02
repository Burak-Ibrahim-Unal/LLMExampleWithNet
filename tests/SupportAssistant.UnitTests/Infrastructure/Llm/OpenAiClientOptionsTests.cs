using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using Knowledge.Infrastructure.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Llm;

/// <summary>
/// OpenAI istemcisinin taşıma ayarlarını (<c>KnowledgeInfrastructureServiceExtensions.CreateClientOptions</c>) gerçek SDK
/// üzerinden, sahte bir HTTP katmanıyla sınar: sunucuya giden istekler sayılır.
/// </summary>
public sealed class OpenAiClientOptionsTests
{
    /// <summary>
    /// Başarısız bir sohbet isteğinin (503) SDK tarafından yeniden gönderilmediğini doğrular: sunucuya tam bir istek gider
    /// ve hata çağırana döner.
    /// </summary>
    /// <remarks>
    /// Soru başına model isteği bütçesi (en fazla iki gerçek istek) handler'da tutulur ve tanılamadaki <c>modelCalls</c>
    /// bu sayıyı gösterir. SDK kendi içinde zaman aşımında ya da 5xx yanıtında isteği yeniden gönderseydi, iki çağrılık
    /// bir soru sunucuya dört istek gönderebilir ve tanılama yine iki gösterirdi. Tek slotlu yerel bir sunucuda yeniden
    /// gönderilen istek, ilk isteğin arkasında kuyruğa girerek yükü de artırırdı.
    /// </remarks>
    [Fact]
    public async Task A_failed_request_is_not_resent_by_the_sdk()
    {
        var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable);
        var options = KnowledgeInfrastructureServiceExtensions.CreateClientOptions("http://model.test/v1", timeoutSeconds: 30);
        options.Transport = new HttpClientPipelineTransport(new HttpClient(handler));
        var chat = new OpenAIClient(new ApiKeyCredential("local"), options).GetChatClient("model");

        await Should.ThrowAsync<ClientResultException>(() =>
            chat.CompleteChatAsync([new UserChatMessage("Merhaba")], cancellationToken: TestContext.Current.CancellationToken));

        handler.Requests.ShouldBe(1);
    }

    /// <summary>
    /// Her isteği sayan ve hep aynı durum koduyla yanıt veren sahte HTTP katmanı.
    /// </summary>
    /// <param name="status">Her isteğe dönülecek durum kodu.</param>
    private sealed class CountingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        /// <summary>Şimdiye kadar gelen istek sayısı.</summary>
        public int Requests { get; private set; }

        /// <summary>İsteği sayar ve gövdesiz bir yanıt döndürür.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") });
        }
    }
}
