using Knowledge.Application.Abstractions;
using Knowledge.Application.Options;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Infrastructure.Search;

/// <summary>
/// <see cref="KnowledgeIndex"/> birim testleri: bellek içi hibrit indeksin (BM25 + kosinüs benzerliği, RRF ile
/// birleştirme) kurulmasını, sürüm kataloğunu, Türkçe karakter toleransını ve vektör aramanın güvenli biçimde BM25'e
/// geri çekildiği durumları doğrular. Gerçek embedding sunucusu yerine deterministik vektörler döndüren
/// <c>FakeTextEmbedder</c> kullanılır; dokümanlar veritabanı olmadan doğrudan domain nesneleri olarak bellekte kurulur.
/// </summary>
public sealed class KnowledgeIndexTests
{
    /// <summary>Verilen embedder ile, varsayılan arama seçeneklerini ve sessiz bir logger'ı kullanan bir indeks kurar.</summary>
    private static KnowledgeIndex CreateIndex(ITextEmbedder embedder) =>
        new(embedder, Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);

    /// <summary>
    /// Test dokümanı kurar: <c>documentKey</c> kimlikle aynıdır; sürüm 1.0, yürürlük 2025-01-01, aktif bir politika.
    /// Her bölüm için bir chunk ekler; vektör verilmişse onu, hangi modelin ürettiği bilgisiyle birlikte chunk'a yazar.
    /// Model adının ayrıca verilebilmesi "başka bir modelin vektörleri" senaryosunu kurmak için gereklidir.
    /// </summary>
    private static KnowledgeDocument Document(string id, string title, params (string Path, string Content, float[]? Vector, string Model)[] sections)
    {
        var document = new KnowledgeDocument(id, id, title, "1.0", new DateOnly(2025, 1, 1), DocumentStatus.Active, DocumentCategory.Policy, null, $"hash-{id}");

        foreach (var section in sections)
        {
            var chunk = document.AddChunk(section.Path, section.Content);

            if (section.Vector is not null)
            {
                chunk.SetEmbedding(section.Vector, section.Model);
            }
        }

        return document;
    }

    /// <summary>İki bölümlü, vektörsüz iade politikası; yalnızca sözcüksel arama senaryolarında kullanılır.</summary>
    private static readonly KnowledgeDocument ReturnPolicy = Document("iade", "İade Politikası",
        ("2. İade Süresi", "Ürünü teslim aldığınız tarihten itibaren 30 gün içinde iade edebilirsiniz.", null, ""),
        ("5. İade Kargo Ücreti", "İade kargosu ücretsizdir.", null, ""));

    /// <summary>
    /// Tek bölümlü, vektörsüz kargo dokümanı. İade politikasının "İade Kargo Ücreti" bölümüyle "kargo" ve "ücret"
    /// sözcüklerini paylaşır; filtre testinde iki dokümanın da sorguyla eşleşmesi, yani filtrenin gerçekten bir şey
    /// elemesi bu sayede sağlanır.
    /// </summary>
    private static readonly KnowledgeDocument Shipping = Document("kargo", "Kargo ve Teslimat",
        ("2. Kargo Ücreti", "750 TL ve üzeri siparişlerde kargo ücretsizdir.", null, ""));

    /// <summary>
    /// İndeksin ilk <c>Rebuild</c> çağrısına kadar hazır sayılmadığını, kurulduktan sonra doküman (2) ve chunk (3)
    /// sayılarını doğru raporladığını doğrular. <c>IsReady</c> soru ve arama akışlarındaki iş kuralını besler: indeks
    /// kurulmadan gelen istekler, anlık görüntü yokken istisna fırlatan <c>Search</c> çağrısına ulaşıp beklenmedik bir
    /// sunucu hatasına düşmek yerine açık bir 503 (<c>IndexNotReady</c>) alır. Sayılar sağlık ve yeniden indeksleme
    /// yanıtlarında raporlanır.
    /// </summary>
    [Fact]
    public void Index_is_not_ready_until_it_is_built()
    {
        var index = CreateIndex(new FakeTextEmbedder(enabled: false));

        index.IsReady.ShouldBeFalse();

        index.Rebuild([ReturnPolicy, Shipping]);

        index.IsReady.ShouldBeTrue();
        index.Status.DocumentCount.ShouldBe(2);
        index.Status.ChunkCount.ShouldBe(3);
    }

    /// <summary>
    /// Aynı <c>documentKey</c>'e sahip tüm sürümlerin (aktif v2 ve yerini yeni sürüme bırakmış v1), indekse yeni sürüm
    /// önce verilse bile yürürlük tarihine göre eskiden yeniye listelendiğini ve bilinmeyen bir aile için boş liste
    /// döndüğünü doğrular.
    /// </summary>
    /// <remarks>
    /// <c>VersionResolver</c> yalnızca aramada eşleşen chunk'ları değil ailenin tüm sürümlerini görmelidir: soru yalnızca
    /// eski sürümle eşleştiğinde güncel sürümün bölümlerini yerine koyabilmek ve elenen sürümü gerekçesiyle
    /// raporlayabilmek bu kataloğa dayanır. Bilinmeyen anahtar için istisna ya da <c>null</c> yerine boş liste dönmesi
    /// çağıranı ek kontrollerden kurtarır.
    /// </remarks>
    [Fact]
    public void Every_version_of_a_document_family_is_listed_oldest_first()
    {
        var index = CreateIndex(new FakeTextEmbedder(enabled: false));
        var current = new KnowledgeDocument("iade-v2", "iade", "İade", "2.0", new DateOnly(2025, 6, 1), DocumentStatus.Active, DocumentCategory.Policy, "iade-v1", "h2");
        current.AddChunk("İade Süresi", "30 gün.");
        var outdated = new KnowledgeDocument("iade-v1", "iade", "İade", "1.0", new DateOnly(2024, 1, 15), DocumentStatus.Superseded, DocumentCategory.Policy, null, "h1");
        outdated.AddChunk("İade Süresi", "14 gün.");
        index.Rebuild([current, outdated, Shipping]);

        index.GetDocumentVersions("iade").Select(version => (version.DocumentId, version.Version, version.Status))
            .ShouldBe([("iade-v1", "1.0", DocumentStatus.Superseded), ("iade-v2", "2.0", DocumentStatus.Active)]);
        index.GetDocumentVersions("yok").ShouldBeEmpty();
    }

    /// <summary>
    /// Türkçe karakter kullanılmadan yazılmış "iade suresi kac gun" sorusunun embedding olmadan (sözcüksel mod)
    /// "2. İade Süresi" bölümünü ilk sırada bulduğunu ve en yüksek kapsamanın 1.0 olduğunu doğrular.
    /// </summary>
    /// <remarks>
    /// Kullanıcılar sık sık Türkçe karakter kullanmadan yazar; normalizasyon iki yazımı aynı terimlere indirger. Ayrıca
    /// "sures" terimi bu bölümde yalnızca başlıkta geçer: doküman başlığı ve bölüm yolunun BM25 metnine katılması
    /// sayesinde kapsama 1.0 olur ve soru Kapı 1'i (kapsama ≥ 0.5) rahatça geçer.
    /// </remarks>
    [Fact]
    public async Task Search_finds_the_section_for_a_question_typed_without_turkish_characters()
    {
        var index = CreateIndex(new FakeTextEmbedder(enabled: false));
        index.Rebuild([ReturnPolicy, Shipping]);

        var result = index.Search(await index.PrepareAsync("iade suresi kac gun", TestContext.Current.CancellationToken), topK: 3);

        result.Mode.ShouldBe(RetrievalMode.Lexical);
        result.Hits[0].Chunk.SectionPath.ShouldBe("2. İade Süresi");
        result.MaxLexicalCoverage.ShouldBe(1.0, 1e-9);
    }

    /// <summary>
    /// Sorguyla ortak sözcüğü olmayan ("paramı geri alırım" ↔ "Para İadesi … kartınıza aktarılır") ama anlamca yakın
    /// bölümün hibrit modda vektör benzerliği sayesinde ilk sıraya geldiğini ve kosinüs skorunun (1.0) hem isabette hem
    /// <c>MaxDenseScore</c>'da raporlandığını doğrular. Vektörler elle seçilmiştir: sorgu [0, 1], "para" [0, 1], "kargo"
    /// [1, 0].
    /// </summary>
    /// <remarks>
    /// Hibrit aramanın varlık nedeni budur: değerlendirmede yalnız BM25 beklenen kaynağı 12 sorunun 10'unda, hibrit arama
    /// 12'sinde buldu. <c>MaxDenseScore</c> Kapı 1'in vektör kanıtıdır (≥ 0.55); sözcüksel kapsama sıfır olsa da bu tür
    /// yeniden ifade edilmiş sorular reddedilmez.
    /// </remarks>
    [Fact]
    public async Task Hybrid_search_also_returns_semantic_matches_without_shared_words()
    {
        var embedder = new FakeTextEmbedder { QueryVector = _ => [0f, 1f] };
        var index = CreateIndex(embedder);
        index.Rebuild(
        [
            Document("para", "Para İadesi", ("Para İadesi", "Ücret 5 iş günü içinde kartınıza aktarılır.", [0f, 1f], FakeTextEmbedder.DefaultModel)),
            Document("kargo", "Kargo", ("Kargo Ücreti", "750 TL üzeri siparişlerde kargo ücretsizdir.", [1f, 0f], FakeTextEmbedder.DefaultModel))
        ]);

        var result = index.Search(await index.PrepareAsync("paramı geri alırım", TestContext.Current.CancellationToken), topK: 2);

        result.Mode.ShouldBe(RetrievalMode.Hybrid);
        result.Hits[0].Chunk.DocumentId.ShouldBe("para");
        result.Hits[0].DenseScore.ShouldNotBeNull();
        result.Hits[0].DenseScore!.Value.ShouldBe(1.0, 1e-6);
        result.MaxDenseScore.ShouldBe(1.0, 1e-6);
    }

    /// <summary>
    /// Saklanan vektörler yapılandırılan modelden ("new-model") farklı bir modelle ("old-model") üretilmişse indeksin bu
    /// vektörleri kullanmadığını, hem arama sonucunun hem indeks durumunun sözcüksel modda kaldığını ve BM25 sonucunun
    /// yine döndüğünü doğrular.
    /// </summary>
    /// <remarks>
    /// Farklı modellerin vektörleri farklı uzaylardadır; karşılaştırılırlarsa anlamsız kosinüs skorları sıralamayı bozar
    /// ve Kapı 1'i yanlışlıkla geçirebilir. Model değiştirildiğinde bir sonraki yeniden indeksleme vektörleri yeni modelle
    /// üretir; o zamana kadar arama güvenli biçimde BM25 ile sürer.
    /// </remarks>
    [Fact]
    public async Task Search_stays_lexical_when_stored_embeddings_come_from_another_model()
    {
        var index = CreateIndex(new FakeTextEmbedder(modelName: "new-model"));
        index.Rebuild([Document("iade", "İade", ("İade Süresi", "30 gün içinde iade edebilirsiniz.", [1f, 0f], "old-model"))]);

        var result = index.Search(await index.PrepareAsync("iade süresi", TestContext.Current.CancellationToken), topK: 3);

        result.Mode.ShouldBe(RetrievalMode.Lexical);
        index.Status.Mode.ShouldBe(RetrievalMode.Lexical);
        result.Hits.ShouldHaveSingleItem();
    }

    /// <summary>
    /// Sorgu vektörünün boyutu (3) indeksteki vektörlerin boyutundan (2) farklıysa sorgu vektörünün atıldığını ve aramanın
    /// hata vermeden, sözcüksel modda doğru bölümü bulduğunu doğrular.
    /// </summary>
    /// <remarks>
    /// Model adı kontrolü, sunucuda aynı ad altında başka bir modelin çalıştırılmasını yakalayamaz; dışarıdan görülebilen
    /// tek belirti boyut farkıdır. <c>PrepareAsync</c> böyle bir vektörü uyarı loglayarak düşürür; aksi hâlde kosinüs
    /// hesabı anlamsız (0) skorlar üretir ve sonuç yine de "hibrit" diye raporlanırdı.
    /// </remarks>
    [Fact]
    public async Task A_query_vector_of_another_dimension_falls_back_to_lexical_search()
    {
        // Embedding sunucusu artık aynı yapılandırılmış ad altında farklı bir model çalıştırıyor.
        var index = CreateIndex(new FakeTextEmbedder { QueryVector = _ => [0f, 1f, 0f] });
        index.Rebuild([Document("iade", "İade", ("İade Süresi", "30 gün içinde iade edebilirsiniz.", [1f, 0f], FakeTextEmbedder.DefaultModel))]);

        var result = index.Search(await index.PrepareAsync("iade süresi", TestContext.Current.CancellationToken), topK: 3);

        result.Mode.ShouldBe(RetrievalMode.Lexical);
        result.Hits.ShouldHaveSingleItem().Chunk.SectionPath.ShouldBe("İade Süresi");
    }

    /// <summary>
    /// İndeks vektörlerle kurulduktan sonra sorgu embedding'i başarısız olursa aramanın istisna fırlatmadan sözcüksel moda
    /// düştüğünü ve doğru bölümü bulduğunu doğrular. İki arıza denenir: sunucuya ulaşılamaması
    /// (<c>HttpRequestException</c>) ve yanıt vermeyen sunucunun zaman aşımı (<c>TaskCanceledException</c>; HTTP
    /// istemcileri zaman aşımını böyle bildirir).
    /// </summary>
    /// <remarks>
    /// Uzak embedding sunucusundaki bir kesinti soru yanıtlamayı durdurmamalıdır; ingest'teki "embedding yoksa yalnızca
    /// BM25" davranışıyla tutarlı olarak sorgu tarafı da zarif biçimde geriler. Zaman aşımı vakası bir hatanın
    /// regresyon testidir: istisna filtresi her <c>OperationCanceledException</c>'ı çağıranın iptali sanıyordu ve asılı
    /// kalan bir sunucu BM25'e düşmek yerine soruyu 500 hatasına götürüyordu.
    /// </remarks>
    [Theory]
    [InlineData("down")]
    [InlineData("timeout")]
    public async Task Search_falls_back_to_lexical_when_the_query_cannot_be_embedded(string failure)
    {
        var embedder = new FakeTextEmbedder();
        var index = CreateIndex(embedder);
        index.Rebuild([Document("iade", "İade", ("İade Süresi", "30 gün içinde iade edebilirsiniz.", [1f, 0f], FakeTextEmbedder.DefaultModel))]);
        embedder.Failure = failure == "down"
            ? new HttpRequestException("embedding server is down")
            : new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");

        var result = index.Search(await index.PrepareAsync("iade süresi", TestContext.Current.CancellationToken), topK: 3);

        result.Mode.ShouldBe(RetrievalMode.Lexical);
        result.Hits.ShouldHaveSingleItem().Chunk.SectionPath.ShouldBe("İade Süresi");
    }

    /// <summary>
    /// Çağıranın kendi iptalinin (iptal edilmiş belirteç) sözcüksel moda düşmeye çevrilmeden yukarı yayıldığını doğrular.
    /// </summary>
    /// <remarks>
    /// Zaman aşımını yutan filtre, iptal edilmiş bir isteği yutmamalıdır: istemci bağlantıyı kapattıysa aramayı sürdürüp
    /// modele gitmek boşa iş olurdu. İki durum, iptalin çağıranın belirtecinden gelip gelmediğine bakılarak ayrılır.
    /// </remarks>
    [Fact]
    public async Task A_cancelled_request_is_not_turned_into_a_lexical_search()
    {
        var embedder = new FakeTextEmbedder();
        var index = CreateIndex(embedder);
        index.Rebuild([Document("iade", "İade", ("İade Süresi", "30 gün içinde iade edebilirsiniz.", [1f, 0f], FakeTextEmbedder.DefaultModel))]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        embedder.Failure = new OperationCanceledException(cancellation.Token);

        await Should.ThrowAsync<OperationCanceledException>(() => index.PrepareAsync("iade süresi", cancellation.Token));
    }

    /// <summary>
    /// Filtre verildiğinde yalnızca filtrenin kabul ettiği chunk'ların döndüğünü doğrular: "ücretsiz kargo" sorgusu iade
    /// politikasının kargo bölümüyle de eşleşir, ama filtre yalnızca kargo dokümanını kabul eder.
    /// </summary>
    /// <remarks>
    /// Soru akışı bu filtreyi, soru yalnızca eski bir sürümle eşleştiğinde güncel sürümün en iyi bölümlerini getirmek için
    /// kullanır. Filtre yok sayılsaydı yerine konan bölümler başka dokümanlardan, hatta elenen eski sürümün kendisinden
    /// gelebilirdi.
    /// </remarks>
    [Fact]
    public async Task Search_only_returns_chunks_accepted_by_the_filter()
    {
        var index = CreateIndex(new FakeTextEmbedder(enabled: false));
        index.Rebuild([ReturnPolicy, Shipping]);

        var result = index.Search(
            await index.PrepareAsync("ücretsiz kargo", TestContext.Current.CancellationToken),
            topK: 5,
            chunk => chunk.DocumentId == "kargo");

        result.Hits.ShouldNotBeEmpty();
        result.Hits.ShouldAllBe(hit => hit.Chunk.DocumentId == "kargo");
    }
}
