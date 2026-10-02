using System.Text;
using System.Text.Json.Nodes;
using Knowledge.Infrastructure.Llm;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Llm;

/// <summary>
/// <see cref="ChatTemplateKwargsPolicy"/> birim testleri. OpenAI SDK'sında llama.cpp/vLLM'in <c>chat_template_kwargs</c>
/// uzantısı için tipli bir alan yoktur; politika bu alanı giden sohbet isteğinin JSON gövdesine kendisi ekler. Testler
/// HTTP pipeline'ı ya da gerçek bir sunucu kurmadan, gövdeyi dönüştüren saf <c>AddEnableThinking</c> fonksiyonunu
/// doğrudan bayt dizileri üzerinde sınar.
/// </summary>
public sealed class ChatTemplateKwargsPolicyTests
{
    /// <summary>
    /// <c>chat_template_kwargs</c> içermeyen bir istek gövdesine bu nesnenin <c>enable_thinking: false</c> değeriyle
    /// eklendiğini ve gövdenin geri kalanının (<c>model</c> vb.) korunduğunu doğrular.
    /// </summary>
    /// <remarks>
    /// Gemma 4'ün düşünme modu sunucuda varsayılan olarak açıktır; değerlendirmede açık düşünme modu doğruluğu artırmadan
    /// medyan yanıt süresini yaklaşık 1,5 sn'den 9,9 sn'ye çıkardı. <c>Llm:EnableThinking</c> ayarı istek gövdesine
    /// ulaşmazsa yapılandırma sessizce etkisiz kalır; gövdenin geri kalanı bozulursa da istek sunucuda reddedilir.
    /// </remarks>
    [Fact]
    public void The_thinking_switch_is_added_to_the_request_body()
    {
        var body = ChatTemplateKwargsPolicy.AddEnableThinking(Encoding.UTF8.GetBytes("""{"model":"gemma","messages":[]}"""), enableThinking: false);

        var json = JsonNode.Parse(body)!;
        json["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>().ShouldBeFalse();
        json["model"]!.GetValue<string>().ShouldBe("gemma");
    }

    /// <summary>
    /// Gövdede zaten bir <c>chat_template_kwargs</c> nesnesi varsa (<c>{"foo": 1}</c>) üzerine yazılmadığını,
    /// <c>enable_thinking</c> anahtarının mevcut argümanlarla birleştirildiğini doğrular. SDK'nın ileride ya da başka bir
    /// pipeline politikasının aynı alana yazacağı şablon argümanlarının sessizce silinmesini önler.
    /// </summary>
    [Fact]
    public void Template_arguments_already_in_the_body_are_kept()
    {
        var body = ChatTemplateKwargsPolicy.AddEnableThinking(Encoding.UTF8.GetBytes("""{"chat_template_kwargs":{"foo":1}}"""), enableThinking: true);

        var arguments = JsonNode.Parse(body)!["chat_template_kwargs"]!;
        arguments["foo"]!.GetValue<int>().ShouldBe(1);
        arguments["enable_thinking"]!.GetValue<bool>().ShouldBeTrue();
    }
}
