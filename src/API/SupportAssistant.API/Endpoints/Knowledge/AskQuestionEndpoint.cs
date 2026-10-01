using FastEndpoints;
using Knowledge.Application.Contracts;
using Knowledge.Service.Abstractions;
using Shared.Application.Common;

namespace SupportAssistant.API.Endpoints.Knowledge;

public sealed class AskQuestionRequest
{
    /// <summary>Turkish support question, at most 500 characters.</summary>
    public string Question { get; set; } = string.Empty;
}

public sealed class AskQuestionEndpoint(IKnowledgeService knowledgeService) : Endpoint<AskQuestionRequest, ApiResult<AnswerDto>>
{
    public override void Configure()
    {
        Post("questions");
        AllowAnonymous();
        Summary(summary =>
        {
            summary.Summary = "Soruyu bilgi tabanındaki dokümanlara dayanarak yanıtlar.";
            summary.Description =
                "Yanıt, kullanılan doküman/sürüm/bölümü ve alıntıyı (sources), eski sürümlerin nasıl elendiğini (versionResolution) " +
                "ve kaynaklar arası çelişkileri (conflicts) içerir. Dokümanlarda yeterli bilgi yoksa answerable=false döner.";
        });
    }

    public override async Task HandleAsync(AskQuestionRequest req, CancellationToken ct)
    {
        var result = await knowledgeService.AskAsync(req.Question, ct);
        await Send.ResponseAsync(result, result.StatusCode, ct);
    }
}
