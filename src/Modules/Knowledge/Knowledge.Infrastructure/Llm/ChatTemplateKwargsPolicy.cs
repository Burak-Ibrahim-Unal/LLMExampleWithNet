using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Knowledge.Infrastructure.Llm;

/// <summary>
/// llama.cpp server (and vLLM) read chat template switches such as Gemma's thinking mode from a non-standard
/// "chat_template_kwargs" request field. The OpenAI SDK has no typed option for it, so this pipeline policy adds
/// the field to outgoing chat completion requests.
/// </summary>
public sealed class ChatTemplateKwargsPolicy(bool enableThinking) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Apply(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Apply(message);
        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
    }

    public static byte[] AddEnableThinking(byte[] requestBody, bool enableThinking)
    {
        var body = JsonNode.Parse(requestBody) as JsonObject
            ?? throw new InvalidOperationException("The chat completion request body is not a JSON object.");

        if (body["chat_template_kwargs"] is not JsonObject templateArguments)
        {
            templateArguments = new JsonObject();
            body["chat_template_kwargs"] = templateArguments;
        }

        templateArguments["enable_thinking"] = enableThinking;
        return JsonSerializer.SerializeToUtf8Bytes(body);
    }

    private void Apply(PipelineMessage message)
    {
        var request = message.Request;

        if (request.Content is null || request.Uri?.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal) != true)
        {
            return;
        }

        using var buffer = new MemoryStream();
        request.Content.WriteTo(buffer);
        request.Content = BinaryContent.Create(BinaryData.FromBytes(AddEnableThinking(buffer.ToArray(), enableThinking)));
    }
}
