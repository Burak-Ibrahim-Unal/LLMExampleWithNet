using Knowledge.Application.Abstractions;
using Knowledge.Application.Contracts;
using MediatR;
using Shared.Application.Common;

namespace Knowledge.Application.Queries.GetSystemStatus;

/// <summary>
/// <see cref="GetSystemStatusQuery"/> isteğini işler. Genel durum, indeks hazır ve dil modeli yapılandırılmışsa
/// <c>"ok"</c>, aksi hâlde <c>"degraded"</c> olur. Embedding bu karara katılmaz: embedding olmadan da sistem BM25-only
/// modda çalışmaya devam eder (yalnızca arama isabeti düşer), bu yüzden embedding'in kapalı olması "bozulmuş" sayılmaz;
/// durumu ayrıca raporlanır.
/// </summary>
/// <remarks>
/// Dış servislere istek atmaz (ping yok), yalnızca yapılandırmayı ve bellekteki indeks durumunu okur: health çağrısı
/// ucuz kalır ve uzak model sunucusu yavaşken takılmaz. Bunun sonucu olarak <c>configured</c> alanı sunucunun o an
/// erişilebilir olduğunu garanti etmez; erişilemeyen dil modeli soru sırasında 503 olarak görünür. Fiilen kullanılan
/// arama modu indeks bölümündeki <c>retrievalMode</c> alanındadır: embedding yapılandırılmış ama indeksleme sırasında
/// sunucuya ulaşılamamışsa orada <c>lexical</c> görünür.
/// </remarks>
public sealed class GetSystemStatusQueryHandler(IKnowledgeIndex index, IGroundedAnswerGenerator generator, ITextEmbedder embedder)
    : IRequestHandler<GetSystemStatusQuery, ApiResult<SystemStatusDto>>
{
    /// <summary>
    /// İndeks durumunu bir kez okuyup sistem durumunu oluşturur. <c>index.Status</c> her erişimde o anki anlık
    /// görüntüden üretildiği için tek bir yerel değişkene alınır; böylece hazır olma bilgisi, sayılar ve arama modu aynı
    /// anlık görüntüden gelir (araya giren bir yeniden indeksleme karışık bir tablo üretemez). İş senkron olduğundan
    /// sonuç <c>Task.FromResult</c> ile döner; async durum makinesine gerek yoktur.
    /// </summary>
    public Task<ApiResult<SystemStatusDto>> Handle(GetSystemStatusQuery request, CancellationToken cancellationToken)
    {
        var indexStatus = index.Status;

        var status = new SystemStatusDto(
            indexStatus.IsReady && generator.IsConfigured ? "ok" : "degraded",
            new IndexStatusDto(indexStatus.IsReady, indexStatus.DocumentCount, indexStatus.ChunkCount, indexStatus.Mode.ToApi(), indexStatus.BuiltAtUtc),
            new ComponentStatusDto(generator.IsConfigured, generator.ModelName),
            new ComponentStatusDto(embedder.IsEnabled, embedder.ModelName));

        return Task.FromResult(ApiResult<SystemStatusDto>.Ok(status));
    }
}
