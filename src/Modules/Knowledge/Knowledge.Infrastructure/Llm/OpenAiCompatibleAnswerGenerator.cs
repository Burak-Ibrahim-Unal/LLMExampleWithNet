using System.Text.Json;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Llm;

/// <summary>
/// Calls an OpenAI-compatible chat endpoint through Microsoft.Extensions.AI and asks for a JSON-schema constrained
/// answer (llama.cpp turns the schema into a grammar, so the reply always parses). An unparsable reply is retried
/// once with a corrective message.
/// </summary>
public sealed class OpenAiCompatibleAnswerGenerator(
    IChatClient chatClient,
    IOptions<LlmOptions> options,
    ILogger<OpenAiCompatibleAnswerGenerator> logger) : IGroundedAnswerGenerator
{
    private const int MaxAttempts = 2;

    // Non-nullable properties stay non-nullable in the generated schema (no ["string","null"] unions).
    private static readonly JsonSerializerOptions SchemaOptions = new(AIJsonUtilities.DefaultOptions)
    {
        RespectNullableAnnotations = true
    };

    public bool IsConfigured => true;

    public string ModelName => options.Value.ChatModel;

    public async Task<GeneratedAnswer> GenerateAsync(string question, IReadOnlyList<ContextChunk> context, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AnswerPrompt.System),
            new(ChatRole.User, AnswerPrompt.BuildUserMessage(question, context))
        };
        var chatOptions = new ChatOptions
        {
            ModelId = settings.ChatModel,
            Temperature = settings.Temperature,
            Seed = settings.Seed,
            MaxOutputTokens = settings.MaxOutputTokens
        };

        for (var attempt = 1; ; attempt++)
        {
            ChatResponse<AnswerPayload> response;

            try
            {
                response = await chatClient.GetResponseAsync<AnswerPayload>(
                    messages,
                    SchemaOptions,
                    chatOptions,
                    useJsonSchemaResponseFormat: settings.UseJsonSchema,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                throw new AnswerGenerationException(AnswerGenerationFailure.Unavailable, $"The language model endpoint failed: {exception.Message}", exception);
            }

            if (response.TryGetResult(out var payload) && payload is not null)
            {
                return ToGeneratedAnswer(payload, response, settings);
            }

            if (attempt == MaxAttempts)
            {
                throw new AnswerGenerationException(AnswerGenerationFailure.InvalidOutput, "The language model did not return the required JSON structure.");
            }

            logger.LogWarning("The language model returned output that does not match the answer schema; retrying once.");
            messages.Add(new ChatMessage(ChatRole.Assistant, response.Text));
            messages.Add(new ChatMessage(ChatRole.User, AnswerPrompt.RetryInstruction));
        }
    }

    private static GeneratedAnswer ToGeneratedAnswer(AnswerPayload payload, ChatResponse response, LlmOptions settings)
    {
        return new GeneratedAnswer(
            payload.Answerable,
            payload.Answer ?? string.Empty,
            (payload.Citations ?? [])
                .Where(citation => !string.IsNullOrWhiteSpace(citation.ChunkId))
                .Select(citation => new GeneratedCitation(citation.ChunkId, citation.Quote ?? string.Empty))
                .ToList(),
            payload.MissingInformation ?? string.Empty,
            (payload.Conflicts ?? [])
                .Select(conflict => new GeneratedConflict(
                    conflict.Topic ?? string.Empty,
                    conflict.ChosenChunkId ?? string.Empty,
                    conflict.RejectedChunkIds ?? [],
                    conflict.Reason ?? string.Empty))
                .ToList(),
            response.ModelId ?? settings.ChatModel,
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount);
    }
}
