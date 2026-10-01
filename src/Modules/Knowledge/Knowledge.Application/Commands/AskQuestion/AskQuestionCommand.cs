using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Commands.AskQuestion;

/// <summary>
/// Answers a support question from the knowledge base. Modelled as a command because it calls an external
/// model (cost) and writes an audit record (<see cref="Knowledge.Domain.Entities.QuestionLog"/>).
/// </summary>
public sealed record AskQuestionCommand(string Question) : IRequest<ApiResult<AnswerDto>>;
