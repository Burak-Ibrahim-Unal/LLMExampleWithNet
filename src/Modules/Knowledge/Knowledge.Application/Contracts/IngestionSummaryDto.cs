namespace Knowledge.Application.Contracts;

/// <summary>
/// Bir ingest (yeniden indeksleme) çalışmasının özeti; <c>POST /v1/documents/reindex</c> yanıtında döner ve açılışta loglanır.
/// </summary>
/// <remarks>
/// Sayaçlar, içerik özetiyle yapılan uzlaştırmanın etkisini görünür kılar: değişmeyen dokümanlar yeniden bölünmez ve
/// yeniden embed edilmez; bu yüzden ardışık iki reindex'in ikincisinde <c>Unchanged</c> yüksek, <c>EmbeddedChunks</c>
/// sıfır olması beklenir.
/// </remarks>
/// <param name="Documents">Ingest sonrasında bilgi tabanındaki doküman sürümü sayısı.</param>
/// <param name="Chunks">İndeksteki toplam bölüm sayısı.</param>
/// <param name="Added">Yeni eklenen dokümanlar.</param>
/// <param name="Updated">İçeriği değiştiği için yeniden bölünen dokümanlar.</param>
/// <param name="Removed">Dosyası artık bulunmadığı için kaldırılan dokümanlar.</param>
/// <param name="Unchanged">İçerik özeti aynı kaldığı için dokunulmayan dokümanlar.</param>
/// <param name="EmbeddedChunks">Bu çalışmada embed edilen bölüm sayısı (vektörü olmayan ya da başka bir modelle üretilmiş bölümler).</param>
/// <param name="RetrievalMode">Bu ingest sonrasında "hybrid" veya "lexical".</param>
/// <param name="Warning">
/// Ingest bozulmuş bir modda başarılı olduğunda dolar (ör. embedding sunucusuna ulaşılamadı ve arama BM25'e düştü);
/// aksi hâlde null.
/// </param>
/// <param name="SuspiciousDocuments">
/// Dil modeline yönelik talimat benzeri metin (dolaylı prompt injection) içeren dokümanların kimlikleri; yoksa boş.
/// Bu dokümanlar indekste kalır, metinleri modele gitmeden etkisizleştirilir; liste operatörün gözden geçirmesi içindir.
/// </param>
public sealed record IngestionSummaryDto(
    int Documents,
    int Chunks,
    int Added,
    int Updated,
    int Removed,
    int Unchanged,
    int EmbeddedChunks,
    string RetrievalMode,
    string? Warning,
    IReadOnlyList<string> SuspiciousDocuments);
