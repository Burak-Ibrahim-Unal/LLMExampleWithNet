using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetDocumentById;

/// <summary>
/// Tek bir doküman sürümünü bölümleriyle birlikte getirme isteği; <c>GET /v1/documents/{id}</c> uç noktasının MediatR
/// karşılığıdır. Bir yanıtın <c>sources</c> alanında görülen doküman kimliğiyle, alıntılanan bölümü dokümanın geri
/// kalanıyla birlikte okumayı sağlar. Sonuç bir <see cref="DocumentDetailDto"/> nesnesidir; kimlik bulunamazsa 404
/// döner.
/// </summary>
/// <param name="Id">Front matter'daki doküman kimliği (ör. <c>iade-politikasi-v2</c>). Aile anahtarı
/// (<c>documentKey</c>) değil belirli bir sürümün kimliğidir; bu sayede yürürlükten kalkmış sürümler de
/// görüntülenebilir. Baştaki ve sondaki boşluklar handler'da kırpılır.</param>
public sealed record GetDocumentByIdQuery(string Id) : IRequest<ApiResult<DocumentDetailDto>>;
