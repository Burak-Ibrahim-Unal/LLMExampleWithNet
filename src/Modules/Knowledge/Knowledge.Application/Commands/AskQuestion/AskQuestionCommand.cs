using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Commands.AskQuestion;

/// <summary>
/// Bir destek sorusunu bilgi tabanındaki dokümanlara dayanarak yanıtlama isteği; <c>POST /v1/questions</c> uç
/// noktasının MediatR karşılığıdır. Sonuç bir <c>ApiResult&lt;AnswerDto&gt;</c> zarfıdır: yanıt da "dokümanlarda
/// yeterli bilgi yok" reddi de HTTP 200 ile döner, ikisini <c>answerable</c> alanı ayırır.
/// </summary>
/// <remarks>
/// Yalnızca okuma yapıyor gibi görünse de bilerek query değil command olarak modellenmiştir: dış bir dil modelini
/// çağırır (maliyet ve gecikme doğurur) ve her soru için bir denetim kaydı
/// (<see cref="Knowledge.Domain.Entities.QuestionLog"/>) yazar. Yani yan etkisi olan, her tekrarında yeniden maliyet
/// doğuran bir işlemdir; CQRS ayrımında yeri command tarafıdır.
/// </remarks>
/// <param name="Question">Kullanıcının Türkçe sorusu. Handler baştaki ve sondaki boşlukları kırpar; soru boş olamaz
/// ve en fazla 500 karakter olabilir (<c>KnowledgeBusinessRules.MaxQueryLength</c>).</param>
public sealed record AskQuestionCommand(string Question) : IRequest<ApiResult<AnswerDto>>;
