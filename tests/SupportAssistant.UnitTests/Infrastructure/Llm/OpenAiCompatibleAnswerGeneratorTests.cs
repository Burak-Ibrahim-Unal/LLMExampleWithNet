using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Llm;

public sealed class OpenAiCompatibleAnswerGeneratorTests
{
    /// <summary>The model endpoint is the external boundary: this replays canned replies and records requests.</summary>
    private sealed class ScriptedChatClient(params string[] replies) : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public List<ChatOptions?> Options { get; } = [];

        public Exception? Failure { get; set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                return Task.FromException<ChatResponse>(Failure);
            }

            Requests.Add(messages.ToList());
            Options.Add(options);
            var reply = replies[Math.Min(Requests.Count - 1, replies.Length - 1)];

            // llama.cpp reports the model file path as the model id.
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply))
            {
                ModelId = "/home/someone/models/gemma.gguf",
                Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 20 }
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private const string ValidReply =
        """{"answerable":true,"answer":"30 gün içinde iade edebilirsiniz.","citations":[{"chunkId":"C1","quote":"30 gün içinde iade edebilir"}],"missingInformation":"","conflicts":[]}""";

    private static readonly IReadOnlyList<ContextChunk> Context =
    [
        new("C1", new IndexedChunk(Guid.NewGuid(), "iade-v2", "iade", "İade ve Para İadesi Politikası", "2.0", new DateOnly(2025, 6, 1),
            DocumentStatus.Active, DocumentCategory.Policy, "2. İade Süresi", "Müşteriler ürünü 30 gün içinde iade edebilir."))
    ];

    private static OpenAiCompatibleAnswerGenerator Create(IChatClient client, LlmOptions? options = null) =>
        new(client, Options.Create(options ?? new LlmOptions { ChatModel = "gemma-test" }), NullLogger<OpenAiCompatibleAnswerGenerator>.Instance);

    [Fact]
    public async Task The_prompt_lists_each_source_with_its_label_document_version_and_section()
    {
        var client = new ScriptedChatClient(ValidReply);

        await Create(client).GenerateAsync("İade süresi kaç gün?", Context, TestContext.Current.CancellationToken);

        var messages = client.Requests.ShouldHaveSingleItem();
        messages[0].Role.ShouldBe(ChatRole.System);
        var prompt = messages.Last(message => message.Role == ChatRole.User).Text;
        prompt.ShouldContain("[C1]");
        prompt.ShouldContain("İade ve Para İadesi Politikası");
        prompt.ShouldContain("sürüm 2.0");
        prompt.ShouldContain("yürürlük 2025-06-01");
        prompt.ShouldContain("tür: politika");
        prompt.ShouldContain("2. İade Süresi");
        prompt.ShouldContain("Müşteriler ürünü 30 gün içinde iade edebilir.");
        prompt.ShouldContain("İade süresi kaç gün?");
    }

    [Fact]
    public async Task The_structured_reply_is_mapped_to_the_generated_answer()
    {
        var answer = await Create(new ScriptedChatClient(ValidReply)).GenerateAsync("İade süresi kaç gün?", Context, TestContext.Current.CancellationToken);

        answer.Answerable.ShouldBeTrue();
        answer.Answer.ShouldBe("30 gün içinde iade edebilirsiniz.");
        answer.Citations.ShouldBe([new GeneratedCitation("C1", "30 gün içinde iade edebilir")]);
        answer.Model.ShouldBe("gemma-test"); // the configured name, not the server's file path
        answer.InputTokens.ShouldBe(100);
        answer.OutputTokens.ShouldBe(20);
    }

    [Fact]
    public async Task Sampling_settings_from_configuration_are_sent_with_the_request()
    {
        var client = new ScriptedChatClient(ValidReply);

        await Create(client, new LlmOptions { ChatModel = "gemma-test", Temperature = 0, Seed = 42, MaxOutputTokens = 3000 })
            .GenerateAsync("İade süresi kaç gün?", Context, TestContext.Current.CancellationToken);

        var options = client.Options.ShouldHaveSingleItem().ShouldNotBeNull();
        options.Temperature.ShouldBe(0f);
        options.Seed.ShouldBe(42);
        options.MaxOutputTokens.ShouldBe(3000);
    }

    [Fact]
    public async Task An_invalid_reply_is_retried_once()
    {
        var client = new ScriptedChatClient("bu json değil", ValidReply);

        var answer = await Create(client).GenerateAsync("İade süresi kaç gün?", Context, TestContext.Current.CancellationToken);

        answer.Answerable.ShouldBeTrue();
        client.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Two_invalid_replies_are_reported_as_invalid_output()
    {
        var generator = Create(new ScriptedChatClient("bozuk", "yine bozuk"));

        var exception = await Should.ThrowAsync<AnswerGenerationException>(
            () => generator.GenerateAsync("İade süresi kaç gün?", Context, TestContext.Current.CancellationToken));

        exception.Failure.ShouldBe(AnswerGenerationFailure.InvalidOutput);
    }

    [Fact]
    public async Task A_transport_failure_is_reported_as_unavailable()
    {
        var client = new ScriptedChatClient(ValidReply) { Failure = new HttpRequestException("connection refused") };

        var exception = await Should.ThrowAsync<AnswerGenerationException>(
            () => Create(client).GenerateAsync("İade süresi kaç gün?", Context, TestContext.Current.CancellationToken));

        exception.Failure.ShouldBe(AnswerGenerationFailure.Unavailable);
    }
}
