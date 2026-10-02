using Knowledge.Application.Contracts;
using Shared.Application.Common;

namespace Knowledge.Service.Abstractions;

/// <summary>
/// Knowledge modülünün API host'una açılan tek giriş noktası (facade): soru yanıtlama, yeniden indeksleme, doküman okuma,
/// arama ve durum sorgusu.
/// </summary>
/// <remarks>
/// Uç noktalar MediatR komut/sorgu tiplerini bilmez, yalnızca bu arayüzü çağırır. Böylece uç noktalar ince adaptör olarak
/// kalır, mesajlaşma altyapısı değişse bile API etkilenmez ve açılıştaki ingestion (<c>Program.cs</c>) uç noktalarla aynı
/// yolu kullanır. Tüm metotlar <see cref="ApiResult{T}"/> döndürür; HTTP durum kodu zarfın içindedir.
/// </remarks>
public interface IKnowledgeService
{
    /// <summary>
    /// Soruyu yalnızca bilgi tabanındaki dokümanlara dayanarak yanıtlar. Yeterli bilgi yoksa 200 ile
    /// <c>answerable=false</c> döner; boş veya 500 karakteri aşan soruda 400, indeks hazır değilse veya dil modeline
    /// ulaşılamıyorsa 503, model geçerli çıktı üretemezse 502.
    /// </summary>
    Task<ApiResult<AnswerDto>> AskAsync(string question, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>knowledge-base/</c> klasörünü yeniden okuyup veritabanı ve arama indeksiyle uzlaştırır; yalnızca değişen
    /// dokümanlar yeniden bölünür ve embed edilir. Hem açılışta hem <c>POST /v1/documents/reindex</c> ile çağrılır.
    /// Bilgi tabanı boş, okunamaz veya geçersizse 422 döner.
    /// </summary>
    Task<ApiResult<IngestionSummaryDto>> ReindexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saklanan tüm doküman sürümlerini (superseded olanlar dahil) sürüm bilgileri ve bölüm sayılarıyla listeler.
    /// </summary>
    Task<ApiResult<List<DocumentSummaryDto>>> ListDocumentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Front matter kimliği (ör. "iade-politikasi-v2") verilen dokümanı tüm bölümleriyle, dosyadaki sırayla döndürür;
    /// yoksa 404. Bir yanıtta kaynak gösterilen bölümün tam metnine bakmayı sağlar.
    /// </summary>
    Task<ApiResult<DocumentDetailDto>> GetDocumentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dil modeli olmadan yalnızca arama yapar ve bulunan bölümleri skorlarıyla döndürür. <paramref name="topK"/>
    /// verilmezse yapılandırmadaki varsayılan kullanılır; <paramref name="mode"/> <c>lexical</c> (yalnızca BM25) veya
    /// <c>hybrid</c> (varsayılan) olabilir. Değerlendirmenin arama başarısını cevap üretiminden ayrı ölçmesini sağlar.
    /// </summary>
    Task<ApiResult<SearchResultDto>> SearchAsync(string query, int? topK, string? mode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// İndeksin, dil modelinin ve embedding yapılandırmasının durumunu döndürür. Her zaman 200 döner; genel durum gövdede
    /// <c>ok</c> veya <c>degraded</c> olarak bildirilir.
    /// </summary>
    Task<ApiResult<SystemStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default);
}
