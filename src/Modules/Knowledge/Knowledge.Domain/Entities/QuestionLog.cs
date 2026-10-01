using Shared.Kernel.Abstractions;

namespace Knowledge.Domain.Entities;

/// <summary>Audit record of an answered (or refused) question, including the full response sent to the client.</summary>
public sealed class QuestionLog : EntityBase
{
    private QuestionLog()
    {
    }

    public QuestionLog(string question, bool answerable, string refusalReason, string model, long latencyMs, string responseJson)
    {
        Question = question;
        Answerable = answerable;
        RefusalReason = refusalReason;
        Model = model;
        LatencyMs = latencyMs;
        ResponseJson = responseJson;
    }

    public string Question { get; private set; } = string.Empty;

    public bool Answerable { get; private set; }

    public string RefusalReason { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public long LatencyMs { get; private set; }

    public string ResponseJson { get; private set; } = string.Empty;
}
