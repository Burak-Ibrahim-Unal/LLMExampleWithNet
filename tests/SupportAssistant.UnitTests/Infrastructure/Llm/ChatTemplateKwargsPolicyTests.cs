using System.Text;
using System.Text.Json.Nodes;
using Knowledge.Infrastructure.Llm;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Llm;

public sealed class ChatTemplateKwargsPolicyTests
{
    [Fact]
    public void The_thinking_switch_is_added_to_the_request_body()
    {
        var body = ChatTemplateKwargsPolicy.AddEnableThinking(Encoding.UTF8.GetBytes("""{"model":"gemma","messages":[]}"""), enableThinking: false);

        var json = JsonNode.Parse(body)!;
        json["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>().ShouldBeFalse();
        json["model"]!.GetValue<string>().ShouldBe("gemma");
    }

    [Fact]
    public void Template_arguments_already_in_the_body_are_kept()
    {
        var body = ChatTemplateKwargsPolicy.AddEnableThinking(Encoding.UTF8.GetBytes("""{"chat_template_kwargs":{"foo":1}}"""), enableThinking: true);

        var arguments = JsonNode.Parse(body)!["chat_template_kwargs"]!;
        arguments["foo"]!.GetValue<int>().ShouldBe(1);
        arguments["enable_thinking"]!.GetValue<bool>().ShouldBeTrue();
    }
}
