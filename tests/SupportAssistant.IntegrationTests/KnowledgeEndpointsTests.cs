using System.Net;
using System.Text.Json;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

/// <summary>
/// Doküman, arama ve yeniden indeksleme uçlarının HTTP sözleşmesini fikstür bilgi tabanı üzerinde uçtan uca doğrular:
/// sürüm meta verisi, bölüm sırası, zarf içinde 404, arama skorları, geçersiz girdide 400 ve değişmemiş içerikte
/// gereksiz yeniden işleme yapılmaması.
/// </summary>
public sealed class KnowledgeEndpointsTests(SupportAssistantApiFactory factory) : IClassFixture<SupportAssistantApiFactory>
{
    /// <summary>
    /// Verilen yöntem ve adresle istek gönderip yanıtı <see cref="ApiResponse"/> olarak döndürür. Her çağrı kendi
    /// <c>HttpClient</c>'ını açıp kapatır; host ise sınıf fikstürü olarak tüm testlerde paylaşılır (açılış indekslemesi bir
    /// kez çalışır). xunit v3'ün <c>TestContext.Current.CancellationToken</c>'ı iletilir ki test iptal edildiğinde ya da
    /// zaman aşımına uğradığında istek de iptal olsun.
    /// </summary>
    private async Task<ApiResponse> SendAsync(HttpMethod method, string url)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(method, url), cancellationToken);
        return await ApiResponse.ReadAsync(response, cancellationToken);
    }

    /// <summary>
    /// <c>GET /v1/documents</c> ucunun üç fikstür dokümanını listelediğini ve yürürlükteki <c>iade-v2</c> için aile
    /// anahtarını (<c>documentKey</c>), sürümü, yürürlük tarihini (<c>yyyy-MM-dd</c>), durumu, kategoriyi ve bölüm sayısını
    /// doğru döndürdüğünü doğrular.
    /// </summary>
    /// <remarks>
    /// Sürüm kararlarının açıklanabilir olması, hangi dokümanın hangi sürüm ve tarihle indekslendiğinin dışarıdan
    /// görülebilmesine bağlıdır. Durum ve kategori, C# enum adlarıyla değil bilgi tabanının front matter sözlüğüyle
    /// (<c>active</c>, <c>politika</c>) yayınlanır; bu sözlük veya tarih biçimi değişirse istemciler kırılır ve bu test
    /// bunu ilk yakalayan yerdir.
    /// </remarks>
    [Fact]
    public async Task Documents_are_listed_with_their_version_metadata()
    {
        using var response = await SendAsync(HttpMethod.Get, "/v1/documents");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetArrayLength().ShouldBe(3);
        var current = response.Data.EnumerateArray().Single(document => document.GetProperty("id").GetString() == "iade-v2");
        current.GetProperty("documentKey").GetString().ShouldBe("iade");
        current.GetProperty("version").GetString().ShouldBe("2.0");
        current.GetProperty("effectiveDate").GetString().ShouldBe("2025-06-01");
        current.GetProperty("status").GetString().ShouldBe("active");
        current.GetProperty("category").GetString().ShouldBe("politika");
        current.GetProperty("sectionCount").GetInt32().ShouldBe(2);
    }

    /// <summary>
    /// <c>GET /v1/documents/iade-v2</c> yanıtında bölümlerin Markdown dosyasındaki sırayla ("2. İade Süresi",
    /// "5. İade Kargo Ücreti") döndüğünü doğrular. Veritabanı satırları belirli bir sırayla gelmek zorunda olmadığından
    /// sıra, bölümlerin ingest sırasında aldığı <c>Order</c> değerine dayanır; bu kırılırsa doküman okuyan kişi bölümleri
    /// karışık görür.
    /// </summary>
    [Fact]
    public async Task Document_details_contain_each_section_in_order()
    {
        using var response = await SendAsync(HttpMethod.Get, "/v1/documents/iade-v2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("sections").EnumerateArray()
            .Select(section => section.GetProperty("section").GetString())
            .ShouldBe(["2. İade Süresi", "5. İade Kargo Ücreti"]);
    }

    /// <summary>
    /// Bilinmeyen bir doküman kimliğinin, çerçevenin gövdesiz 404'ü yerine <c>ApiResult</c> zarfı içinde 404,
    /// <c>success = false</c> ve Türkçe "Doküman bulunamadı." mesajıyla döndüğünü doğrular. İstemci başarılı ya da hatalı
    /// her yanıtı aynı biçimde ayrıştırabilmelidir; test ayrıca 404'ün iş kuralından (<c>CheckDocumentFound</c>) geldiğini
    /// gösterir.
    /// </summary>
    [Fact]
    public async Task An_unknown_document_returns_404_in_the_envelope()
    {
        using var response = await SendAsync(HttpMethod.Get, "/v1/documents/yok");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Success.ShouldBeFalse();
        response.Message.ShouldBe("Doküman bulunamadı.");
    }

    /// <summary>
    /// "iade süresi" araması (<c>topK = 2</c>) için BM25 modunda (<c>lexical</c>) en iyi iki sonucun döndüğünü doğrular:
    /// ikisi de "2. İade Süresi" bölümüdür, biri <c>iade-v1</c>'den biri <c>iade-v2</c>'den gelir ve ilk sonucun kelime
    /// kapsamı (<c>lexicalCoverage</c>) 1.0'dır.
    /// </summary>
    /// <remarks>
    /// Arama ucu yalnızca erişimi gösterir ve sürüm çözümü uygulamaz; eski sürümün de listelenmesi bu yüzden beklenen
    /// davranıştır. Değerlendirme aracı arama isabetini bu uçla, üretimden ayrı ölçer. Kelime kapsamı Kapı 1'in kullandığı
    /// metriktir (sorgu terimlerinin tek bir bölümde bulunan idf ağırlıklı payı); ham BM25 skorları sorgular arasında
    /// karşılaştırılamadığı için eşik bu metrik üzerinden tanımlıdır. İki sürümün metni neredeyse aynı olduğundan
    /// aralarındaki sıra test edilmez (<c>ignoreOrder</c>).
    /// </remarks>
    [Fact]
    public async Task Search_returns_ranked_sections_with_their_scores()
    {
        using var response = await SendAsync(HttpMethod.Get, "/v1/search?q=iade%20s%C3%BCresi&topK=2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("retrievalMode").GetString().ShouldBe("lexical");
        var hits = response.Data.GetProperty("hits").EnumerateArray().ToList();
        hits.Count.ShouldBe(2);
        hits.ShouldAllBe(hit => hit.GetProperty("section").GetString() == "2. İade Süresi");
        hits.Select(hit => hit.GetProperty("documentId").GetString()).ShouldBe(["iade-v1", "iade-v2"], ignoreOrder: true);
        hits[0].GetProperty("lexicalCoverage").GetDouble().ShouldBe(1.0, 1e-9);
    }

    /// <summary>
    /// Geçersiz arama girdisinin (boş sorgu, aralık dışı <c>topK</c>) 400 ve ilgili Türkçe iş kuralı mesajıyla
    /// reddedildiğini doğrular. Geçersiz girdi bir istemci hatasıdır ve öyle raporlanmalıdır; mesaj, kullanıcıya neyi
    /// düzelteceğini söyler. Kurallar fail-fast çalışır: geçersiz bir istek indekse hiç ulaşmaz.
    /// </summary>
    [Theory]
    [InlineData("/v1/search?q=", "Arama ifadesi boş olamaz.")]
    [InlineData("/v1/search?q=iade&topK=0", "topK 1 ile 20 arasında olmalıdır.")]
    public async Task Search_rejects_invalid_input(string url, string expectedMessage)
    {
        using var response = await SendAsync(HttpMethod.Get, url);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Message.ShouldBe(expectedMessage);
    }

    /// <summary>
    /// Açılışta indekslenmiş ve dosyaları değişmemiş bilgi tabanında <c>POST /v1/documents/reindex</c> çağrısının üç
    /// dokümanı da <c>unchanged</c> saydığını ve hiçbirini yeniden eklemediğini doğrular.
    /// </summary>
    /// <remarks>
    /// Veritabanıyla mutabakat içerik hash'iyle yapılır: değişmeyen doküman yeniden bölümlenmez ve yeniden embed edilmez.
    /// Bu kırılırsa her reindex'te tüm bölümler uzak ve yavaş embedding sunucusuna yeniden gönderilir. Test paylaşılan
    /// host'un durumunu değiştirmediği için sınıftaki diğer testlerle hangi sırada çalıştığı önemli değildir.
    /// </remarks>
    [Fact]
    public async Task Reindexing_an_unchanged_knowledge_base_keeps_every_document()
    {
        using var response = await SendAsync(HttpMethod.Post, "/v1/documents/reindex");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("documents").GetInt32().ShouldBe(3);
        response.Data.GetProperty("unchanged").GetInt32().ShouldBe(3);
        response.Data.GetProperty("added").GetInt32().ShouldBe(0);
    }
}

/// <summary>
/// Bilgi tabanı klasörü bulunamadığında (<see cref="MissingKnowledgeBaseApiFactory"/>) arama ve yeniden indeksleme
/// uçlarının çökmeden, anlamlı durum kodları ve mesajlarla yanıt verdiğini doğrular.
/// </summary>
public sealed class MissingKnowledgeBaseTests(MissingKnowledgeBaseApiFactory factory) : IClassFixture<MissingKnowledgeBaseApiFactory>
{
    /// <summary>
    /// İndeks hiç kurulamamışken aramanın 503 ve <c>success = false</c> döndürdüğünü doğrular. Boş bir sonuç listesi
    /// dönseydi istemci "eşleşme yok" sanırdı; 503 ise sorunun sunucu tarafında ve geçici olduğunu, başarılı bir yeniden
    /// indekslemeden sonra düzeleceğini anlatır.
    /// </summary>
    [Fact]
    public async Task Search_reports_service_unavailable_while_the_index_is_not_built()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await ApiResponse.ReadAsync(await client.GetAsync("/v1/search?q=iade", cancellationToken), cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Success.ShouldBeFalse();
    }

    /// <summary>
    /// Klasör yokken yeniden indekslemenin 422, "Bilgi tabanı klasörü bulunamadı" mesajı ve <c>data = null</c> ile
    /// döndüğünü doğrular. Operatör yanlış yapılandırmayı (<c>KnowledgeBase:Path</c>) yanıttan anlayabilmeli; hata yine
    /// standart zarf içinde gelmeli ve yakalanmamış bir istisnaya (500) dönüşmemelidir.
    /// </summary>
    [Fact]
    public async Task Reindex_explains_why_the_knowledge_base_cannot_be_read()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await ApiResponse.ReadAsync(await client.PostAsync("/v1/documents/reindex", null, cancellationToken), cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Message.ShouldContain("Bilgi tabanı klasörü bulunamadı");
        response.Data.ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
