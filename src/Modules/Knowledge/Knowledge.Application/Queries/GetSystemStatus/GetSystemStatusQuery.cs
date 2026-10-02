using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetSystemStatus;

/// <summary>
/// Sistemin çalışma durumunu sorgulama isteği; <c>GET /v1/health</c> uç noktasının MediatR karşılığıdır. İndeksin hazır
/// olup olmadığını, fiilen kullanılan arama modunu (hybrid/lexical) ve dil modeli ile embedding bileşenlerinin
/// yapılandırılıp yapılandırılmadığını tek yanıtta döndürür (<see cref="SystemStatusDto"/>). Böylece "sorular neden 503
/// alıyor?" ya da "arama neden yalnızca BM25 ile çalışıyor?" gibi sorular tek bir çağrıyla teşhis edilebilir.
/// </summary>
public sealed record GetSystemStatusQuery : IRequest<ApiResult<SystemStatusDto>>;
