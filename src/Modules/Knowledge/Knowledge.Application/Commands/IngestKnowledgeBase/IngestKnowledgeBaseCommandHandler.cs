using Knowledge.Application.Abstractions;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Application.Exceptions;
using Knowledge.Application.Security;
using Knowledge.Domain.Entities;
using Knowledge.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Application.Common;

namespace Knowledge.Application.Commands.IngestKnowledgeBase;

/// <summary>
/// Veritabanındaki dokümanları bilgi tabanı dosyalarıyla uzlaştırır: değişmeyen dosyalar bölümlerini ve
/// embedding'lerini korur, değişen dosyalar yeniden bölümlenip yeniden embed edilir, artık var olmayan dosyaların
/// kayıtları silinir. Ardından bellek içi arama indeksi kaydedilen durumdan yeniden kurulur.
/// </summary>
/// <remarks>
/// Uzlaştırma iki anahtara dayanır: dosyanın içerik özeti (<c>ContentHash</c>; kaynak adaptörü onu front matter dahil
/// dosyanın tamamından hesaplar) ve vektörü üreten embedding modelinin adı. Böylece uzak ve yavaş embedding sunucusu
/// yalnızca gerçekten gereken bölümler için çağrılır: her açılışta bilgi tabanının tamamını yeniden embed etmek
/// gerekmez, embedding modeli değiştiğinde ise eski vektörler kendiliğinden yenilenir. Embedding sunucusu erişilemezse
/// indeksleme yine başarılı olur ve arama BM25-only moda düşer (<c>EmbeddingUnavailable</c> uyarısı). Veritabanı
/// türetilmiş veridir; doğruluk kaynağı her zaman Markdown dosyalarıdır.
/// </remarks>
public sealed class IngestKnowledgeBaseCommandHandler(
    IKnowledgeBaseSource source,
    IKnowledgeDocumentRepository repository,
    ITextEmbedder embedder,
    IKnowledgeIndex index,
    KnowledgeBusinessRules rules,
    ILogger<IngestKnowledgeBaseCommandHandler> logger) : IRequestHandler<IngestKnowledgeBaseCommand, ApiResult<IngestionSummaryDto>>
{
    /// <summary>
    /// Yeniden indekslemeyi süreç genelinde aynı anda tek bir çalışmayla sınırlayan kilit. Yeniden indeksleme ara sıra
    /// yapılan bir operatör işlemidir; aynı anda gelen iki istek (ör. açılıştaki indeksleme ile
    /// <c>POST /v1/documents/reindex</c>) aynı bölümleri birlikte değiştirmeye kalkarsa yavaş olanı, diğerinin çoktan
    /// sildiği satırları silmeye çalışırken eşzamanlılık hatasıyla (<c>DbUpdateConcurrencyException</c>) düşer. Bu
    /// yarış kod incelemesinde bulundu ve eşzamanlı yeniden indeksleme testiyle korunuyor.
    /// </summary>
    /// <remarks>
    /// Statiktir, çünkü MediatR handler'ı her istek için yeniden oluşturur ve her isteğin kendi DbContext'i vardır;
    /// örnek alanı olarak tutulan bir kilit istekler arasında paylaşılmazdı. <c>lock</c> yerine
    /// <see cref="SemaphoreSlim"/> seçildi, çünkü kilidin <c>await</c> noktaları boyunca (dosya okuma, embedding çağrısı,
    /// veritabanı kaydı) tutulması gerekir ve <c>WaitAsync</c> bekleyen isteği bir iş parçacığını bloklamadan ve iptal
    /// edilebilir biçimde bekletir. Tek süreçte çalışan bu uygulama için yeterlidir; birden çok örnek aynı veritabanını
    /// paylaşsaydı dağıtık bir kilit gerekirdi.
    /// </remarks>
    private static readonly SemaphoreSlim IngestionGate = new(1, 1);

    /// <summary>
    /// Kilidi (<see cref="IngestionGate"/>) alır, asıl işi <see cref="IngestAsync"/> içinde yapar ve kilidi her durumda
    /// <c>finally</c> ile bırakır. Kilit yönetimi iş mantığından ayrı tutulur: <c>try/finally</c> tek bir çağrıyı sarar,
    /// böylece <see cref="IngestAsync"/> içindeki erken <c>return</c> yolları da fırlayan istisnalar da kilidi açık
    /// bırakamaz ve iş mantığı kilit ayrıntısıyla karışmaz.
    /// </summary>
    /// <remarks>
    /// Aynı anda gelen ikinci istek reddedilmez, sırasını bekler. Sıra ona geldiğinde ilkinin kaydettiği durumu okur ve
    /// büyük olasılıkla her şeyi "değişmemiş" bulur; bu yüzden beklemek ucuzdur. Bekleme
    /// <paramref name="cancellationToken"/> ile iptal edilebilir; kilit alınmadan iptal edilirse bırakılacak bir şey de
    /// yoktur, çünkü bekleme <c>try</c> bloğunun dışındadır.
    /// </remarks>
    public async Task<ApiResult<IngestionSummaryDto>> Handle(IngestKnowledgeBaseCommand request, CancellationToken cancellationToken)
    {
        await IngestionGate.WaitAsync(cancellationToken);

        try
        {
            return await IngestAsync(cancellationToken);
        }
        finally
        {
            IngestionGate.Release();
        }
    }

    /// <summary>
    /// Asıl uzlaştırma: dosyaları okur, iş kurallarını uygular (bilgi tabanı boş olamaz, doküman kimlikleri benzersiz
    /// olmalı), her dosyayı veritabanındaki kaydıyla <c>SourceId</c> üzerinden eşleştirir; içerik özeti aynıysa kaydı
    /// olduğu gibi bırakır, farklıysa meta veriyi günceller ve bölümleri yeniden oluşturur, yeni dosyalar için kayıt
    /// ekler, dosyası kalmayan kayıtları siler. Sonra eksik vektörleri tamamlar, tek seferde kaydeder ve indeksi yeniden
    /// kurar.
    /// </summary>
    /// <remarks>
    /// Hata eşlemesi: biçimi bozuk bir dosya (<see cref="KnowledgeBaseFormatException"/>) 422 ile ve dosya adını içeren
    /// kendi mesajıyla döner, çünkü bu mesaj operatöre neyi düzelteceğini söyler. Okuma hataları
    /// (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>) da 422'dir, ancak istemciye yalnızca genel
    /// <c>KnowledgeBaseUnreadable</c> mesajı gider; ayrıntı (dosya yolları, izin bilgisi) yalnızca sunucu loguna yazılır,
    /// böylece sunucunun dosya sistemi hakkında bilgi dışarı sızmaz. Herhangi bir hatada indekse dokunulmaz: önceki
    /// anlık görüntü hizmet vermeye devam eder, açılışta ise indeks hazır olmaz ve sorular yeniden indeksleme başarılı
    /// olana kadar 503 alır. İndeks ancak kayıt başarılı olduktan sonra yenilenir; böylece hiçbir zaman veritabanına
    /// yazılmamış bir durumu yansıtmaz.
    /// </remarks>
    private async Task<ApiResult<IngestionSummaryDto>> IngestAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SourceDocument> sources;

        try
        {
            sources = await source.LoadAsync(cancellationToken);
        }
        catch (KnowledgeBaseFormatException exception)
        {
            // Mesaj dosya adını ve sorunu söyler (ör. eksik front matter alanı); operatöre neyi düzelteceğini gösterir.
            return ApiResult<IngestionSummaryDto>.Fail(exception.Message, 422);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Ayrıntı (dosya yolu, izin bilgisi) yalnızca sunucu loguna yazılır; istemci genel bir mesaj alır.
            logger.LogError(exception, "The knowledge base files could not be read.");
            return ApiResult<IngestionSummaryDto>.Fail(Messages.Knowledge.KnowledgeBaseUnreadable, 422);
        }

        var emptyError = rules.CheckKnowledgeBaseNotEmpty<IngestionSummaryDto>(sources.Count);
        if (emptyError is not null)
        {
            return emptyError;
        }

        var duplicateError = rules.CheckUniqueDocumentIds<IngestionSummaryDto>(sources.Select(document => document.SourceId));
        if (duplicateError is not null)
        {
            return duplicateError;
        }

        // Dolaylı prompt injection: dil modeline yönelik talimat benzeri metin içeren dokümanlar uyarıyla raporlanır ama
        // indeksten çıkarılmaz. Yanlış bir alarm gerçek bir politikayı aramadan düşürürdü; metin modele gitmeden zaten
        // etkisizleştirilir ve yanıtlar doğrulanmış alıntıya dayanmak zorundadır.
        var suspicious = sources.Where(ContainsInstructionLikeText).Select(document => document.SourceId).ToList();

        foreach (var documentId in suspicious)
        {
            logger.LogWarning(
                "Document {DocumentId} contains instruction-like text (possible prompt injection); it stays indexed and its text is neutralized before it reaches the model.",
                documentId);
        }

        // Veritabanındaki mevcut durum. Eşleşen her kayıt döngüde sözlükten çıkarılır; döngü bittiğinde sözlükte kalanlar
        // dosyası artık olmayan, silinecek dokümanlardır.
        var stored = (await repository.ListWithChunksAsync(cancellationToken))
            .ToDictionary(document => document.SourceId, StringComparer.OrdinalIgnoreCase);
        var current = new List<KnowledgeDocument>(sources.Count);
        int added = 0, updated = 0, unchanged = 0;

        foreach (var document in sources)
        {
            if (stored.Remove(document.SourceId, out var existing))
            {
                current.Add(existing);

                // İçerik özeti aynı: kayıtlı bölümler ve vektörleri hâlâ geçerli, yeniden bölümleme ve embed maliyeti yok.
                if (existing.ContentHash == document.ContentHash)
                {
                    unchanged++;
                    continue;
                }

                // Dosya değişmiş: meta veri güncellenir, bölümler baştan oluşturulur. Yeni bölümlerin vektörü olmadığından
                // aşağıdaki embedding adımı onları kendiliğinden yakalar.
                existing.Revise(document.DocumentKey, document.Title, document.Version, document.EffectiveDate, document.Status, document.Category, document.Supersedes, document.ContentHash);
                existing.ClearChunks();
                AddSections(existing, document);
                updated++;
                continue;
            }

            var created = new KnowledgeDocument(document.SourceId, document.DocumentKey, document.Title, document.Version, document.EffectiveDate, document.Status, document.Category, document.Supersedes, document.ContentHash);
            AddSections(created, document);
            await repository.AddAsync(created, cancellationToken);
            current.Add(created);
            added++;
        }

        foreach (var removed in stored.Values)
        {
            repository.Remove(removed);
        }

        // Vektörler kayıttan önce tamamlanır; böylece bölümler ve embedding'leri tek bir SaveChanges ile birlikte yazılır.
        var (embeddedChunks, warning) = await EmbedMissingChunksAsync(current, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        // İndeks yalnızca kayıt başarılı olduktan sonra yeniden kurulur; okuyucular yeni anlık görüntüye tek bir atamayla
        // geçer, yarım kurulmuş bir indeks görmez.
        index.Rebuild(current);
        var status = index.Status;

        var summary = new IngestionSummaryDto(
            Documents: current.Count,
            Chunks: status.ChunkCount,
            Added: added,
            Updated: updated,
            Removed: stored.Count,
            Unchanged: unchanged,
            EmbeddedChunks: embeddedChunks,
            RetrievalMode: status.Mode == RetrievalMode.Hybrid ? "hybrid" : "lexical",
            Warning: warning,
            SuspiciousDocuments: suspicious);

        return ApiResult<IngestionSummaryDto>.Ok(summary, Messages.Knowledge.Reindexed);
    }

    /// <summary>
    /// Bir dokümanın başlığında, bölüm yollarında ya da bölüm metinlerinde dil modeline yönelik talimat benzeri metin
    /// (dolaylı prompt injection) olup olmadığını <see cref="PromptInjectionDetector"/> ile denetler.
    /// </summary>
    /// <remarks>
    /// Sorulardaki doğrudan saldırının dokümanlar üzerinden yapılan karşılığıdır: bilgi tabanına giren bir yönerge
    /// (kopyala-yapıştır ya da ele geçirilmiş bir kaynak), modele verilen bağlamın içinden talimat vermeye çalışabilir.
    /// </remarks>
    private static bool ContainsInstructionLikeText(SourceDocument document) =>
        PromptInjectionDetector.Detect(document.Title) is not null
        || document.Sections.Any(section =>
            PromptInjectionDetector.Detect(section.SectionPath) is not null || PromptInjectionDetector.Detect(section.Content) is not null);

    /// <summary>
    /// Kaynak dosyanın bölümlerini dokümana dosyadaki sırasıyla ekler. Bölümleme kaynak adaptöründe yapılmıştır (her
    /// başlık bir bölüm; uzun bölümler paragraf sınırından bölünür), burada yalnızca aggregate'e aktarılır. Ekleme
    /// aggregate üzerinden (<c>AddChunk</c>) yapılır, çünkü <see cref="DocumentChunk"/> kurucusu <c>internal</c>'dır:
    /// sıra numarasını ve doküman bağını yalnızca aggregate atar, tutarsız bir bölüm dışarıda oluşturulamaz.
    /// </summary>
    private static void AddSections(KnowledgeDocument document, SourceDocument source)
    {
        foreach (var section in source.Sections)
        {
            document.AddChunk(section.SectionPath, section.Content);
        }
    }

    /// <summary>
    /// Yapılandırılmış embedding modelinin ürettiği bir vektörü olmayan bölümleri embed eder: hiç vektörü olmayanlar
    /// (yeni ya da değişmiş dosyalar, önceki bir denemede embed edilemeyenler) ve vektörü başka bir modelle üretilmiş
    /// olanlar. Farklı modellerin vektörleri aynı uzayda olmadığından karşılaştırılamaz; model adı kontrolü sayesinde
    /// model değiştiğinde eski vektörler kendiliğinden yenilenir. Embed edilen bölüm sayısını ve (varsa) özette
    /// gösterilecek uyarıyı döndürür.
    /// </summary>
    /// <remarks>
    /// Erişilemeyen bir embedding sunucusu indekslemeyi başarısız kılmaz: hata loglanır, <c>EmbeddingUnavailable</c>
    /// uyarısı döner, vektörü eksik bölümler o hâliyle kaydedilir ve indeks BM25-only modda kurulur (indeks vektörleri
    /// ancak tüm bölümlerde aynı modelden geliyorsa kullanır); sonraki bir yeniden indeksleme eksik vektörleri tamamlar. Bilgi tabanının hiç aranamaması, vektör aramasının getirdiği ek isabetten çok daha
    /// kötü bir sonuç olurdu. Yanıt vermeyen sunucunun zaman aşımı da bu kapsamdadır; HTTP istemcileri onu
    /// <see cref="OperationCanceledException"/> olarak bildirdiği için gerçek iptalden belirtecin durumuna bakılarak ayrılır.
    /// Çağıranın kendi iptali (belirteç iptal edilmişse) kapsam dışıdır: o bir sunucu hatası değil çağıranın isteğidir ve
    /// yukarı iletilmelidir. Embedding kapalıysa (<c>Embeddings:BaseUrl</c> boş)
    /// hiçbir şey yapılmaz. Bekleyen bölümlerin tamamı tek çağrıyla adaptöre verilir; isteklerin gruplara bölünmesi
    /// adaptörün işidir.
    /// </remarks>
    private async Task<(int EmbeddedChunks, string? Warning)> EmbedMissingChunksAsync(IReadOnlyList<KnowledgeDocument> documents, CancellationToken cancellationToken)
    {
        if (!embedder.IsEnabled)
        {
            return (0, null);
        }

        var pending = documents
            .SelectMany(document => document.Chunks.Select(chunk => (Document: document, Chunk: chunk)))
            .Where(entry => entry.Chunk.Embedding is null || entry.Chunk.EmbeddingModel != embedder.ModelName)
            .ToList();

        if (pending.Count == 0)
        {
            return (0, null);
        }

        try
        {
            var vectors = await embedder.EmbedDocumentsAsync(
                pending.Select(entry => new DocumentEmbeddingInput(entry.Document.Title, EmbeddingText(entry.Document, entry.Chunk))).ToList(),
                cancellationToken);

            for (var i = 0; i < pending.Count; i++)
            {
                pending[i].Chunk.SetEmbedding(vectors[i], embedder.ModelName);
            }

            return (pending.Count, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Zaman aşımı da buraya düşer: HTTP istemcileri onu OperationCanceledException olarak bildirir, ama çağıran
            // iptal etmediği için bu bir sunucu arızasıdır ve ingest BM25 ile tamamlanır. Yalnızca gerçek iptal yayılır.
            logger.LogWarning(exception, "Embedding {ChunkCount} chunks failed; the index will use BM25 only.", pending.Count);
            return (0, Messages.Knowledge.EmbeddingUnavailable);
        }
    }

    /// <summary>
    /// Bir bölümün embedding'e gönderilecek metnini oluşturur: önce <c>Başlık &gt; Bölüm yolu</c> satırı, ardından bölüm
    /// içeriği. Başlık ve bölüm yolu, kısa bir bölüme ("2. İade Süresi" altındaki tek bir cümle gibi) bulunabilmesi için
    /// gereken bağlamı verir; yalnızca içerik embed edilseydi "30 gün içinde..." cümlesinin hangi konuya ait olduğu
    /// vektöre yansımazdı. Aynı fikir BM25 tarafında da uygulanır: başlık ve bölüm yolu indekslenen metne katılır.
    /// </summary>
    private static string EmbeddingText(KnowledgeDocument document, DocumentChunk chunk) =>
        $"{document.Title} > {chunk.SectionPath}\n{chunk.Content}";
}
