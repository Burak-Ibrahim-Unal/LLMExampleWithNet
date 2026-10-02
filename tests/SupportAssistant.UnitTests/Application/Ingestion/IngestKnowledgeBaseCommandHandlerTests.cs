using Knowledge.Application.Abstractions;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Commands.IngestKnowledgeBase;
using Knowledge.Application.Contracts;
using Knowledge.Application.Exceptions;
using Knowledge.Application.Options;
using Knowledge.Infrastructure.Persistence;
using Knowledge.Infrastructure.Search;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Persistence;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Ingestion;

/// <summary>
/// <see cref="IngestKnowledgeBaseCommandHandler"/> gerçek (bellek içi) bir SQLite veritabanına ve gerçek indekse karşı
/// çalıştırılır; yalnızca embedding sunucusu (<see cref="FakeTextEmbedder"/>) taklit edilir. Markdown okuyucusunun yerini
/// ise dokümanları bellekte tanımlayan <see cref="StubKnowledgeBaseSource"/> alır.
/// </summary>
/// <remarks>
/// Ingestion'ın asıl işi, dosyalardaki dokümanları veritabanıyla içerik özeti (content hash) ve embedding model adı
/// üzerinden uzlaştırmaktır: değişmeyen doküman yeniden embed edilmez (embedding sunucusu uzak ve yavaştır), değişen
/// doküman yeniden bölünüp embed edilir, silinen doküman kaldırılır. Bu davranışlar ancak gerçek EF eşlemeleri ve gerçek
/// bir veritabanıyla anlamlı biçimde sınanabilir; hata yolları (bozuk, okunamayan, boş bilgi tabanı) da burada korunur.
/// </remarks>
public sealed class IngestKnowledgeBaseCommandHandlerTests : IAsyncLifetime
{
    /// <summary>
    /// Bellek içi SQLite bağlantısı; veritabanı yalnızca bu bağlantı açıkken yaşadığından test boyunca açık tutulur. Her
    /// ingestion yeni bir context açsa da hepsi aynı veritabanını görür; böylece "uygulama yeniden başladı" senaryosu
    /// kurulabilir.
    /// </summary>
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    /// <summary>Bilgi tabanı dosyalarının yerine geçen stub; testler kaynağı <c>Documents</c> ve <c>Failure</c> ile belirler.</summary>
    private readonly StubKnowledgeBaseSource _source = new();
    /// <summary>
    /// Embedding sunucusunun yerine geçen, varsayılan olarak etkin fake; bu yüzden başarılı ingestion'lardan sonra indeks
    /// hibrit modda çalışır. <c>Failure</c> ile sunucuya ulaşılamayan durum taklit edilir.
    /// </summary>
    private readonly FakeTextEmbedder _embedder = new();
    /// <summary>Handler'ın ingestion sonunda yeniden kurduğu gerçek indeks; testler <c>Status</c> ve <c>IsReady</c> değerlerini denetler.</summary>
    private KnowledgeIndex _index = null!;

    /// <summary>İki bölümlük örnek iade dokümanı (içerik özeti <c>hash-iade-1</c>); testlerde hiç değişmeyen doküman rolündedir.</summary>
    private static readonly SourceDocument ReturnPolicy = StubKnowledgeBaseSource.Document("iade", "hash-iade-1",
        new SourceSection("İade Süresi", "30 gün içinde iade edebilirsiniz."),
        new SourceSection("Para İadesi", "Ücret 5 iş günü içinde iade edilir."));

    /// <summary>
    /// Tek bölümlük örnek kargo dokümanı (içerik özeti <c>hash-kargo-1</c>); değişiklik ve silme testlerinde değiştirilen
    /// ya da kaldırılan doküman rolündedir.
    /// </summary>
    private static readonly SourceDocument Shipping = StubKnowledgeBaseSource.Document("kargo", "hash-kargo-1",
        new SourceSection("Kargo Ücreti", "750 TL üzeri ücretsiz."));

    /// <summary>Bağlantıyı açar, indeksi oluşturur ve şemayı <c>EnsureCreatedAsync</c> ile hazırlar.</summary>
    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        _index = new KnowledgeIndex(_embedder, Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>Bağlantıyı kapatır; bellek içi veritabanı da onunla birlikte silinir ve testler birbirinden izole kalır.</summary>
    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Paylaşılan bağlantı üzerinde yeni bir <c>AppDbContext</c> oluşturur; Knowledge modülünün EF yapılandırmaları
    /// üretimdeki gibi <c>EntityConfigurationAssemblyRegistry</c> ile eklenir.
    /// </summary>
    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options,
        new EntityConfigurationAssemblyRegistry([Knowledge.Infrastructure.AssemblyReference.Assembly]));

    /// <summary>Her çağrı, yeni bir istek ya da uygulamanın yeniden başlaması gibi taze bir <c>DbContext</c> kullanır.</summary>
    /// <remarks>
    /// Böylece sonraki ingestion, önceki çağrının change tracker'ında kalan nesneleri değil veritabanından yeniden yüklenen
    /// kayıtları uzlaştırır. Değişen bir dokümana yeni chunk eklenmesi de üretimdeki gibi veritabanından yüklenip izlenen
    /// (tracked) bir dokümanın koleksiyonu üzerinden gerçekleşir.
    /// </remarks>
    private async Task<Shared.Application.Common.ApiResult<IngestionSummaryDto>> IngestAsync()
    {
        await using var context = CreateContext();
        var handler = new IngestKnowledgeBaseCommandHandler(
            _source,
            new KnowledgeDocumentRepository(context),
            _embedder,
            _index,
            new KnowledgeBusinessRules(_index),
            NullLogger<IngestKnowledgeBaseCommandHandler>.Instance);

        return await handler.Handle(new IngestKnowledgeBaseCommand(), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Veritabanında gerçekten kayıtlı olan chunk'ları yeni bir context ile okur ve (kaynak kimliği, bölüm yolu, içerik,
    /// embedding var mı) demetleri olarak döndürür. Doküman içi sıra korunarak kaynak kimliğine göre sıralanır; böylece
    /// beklenen değerler okunaklı bir liste olarak birebir karşılaştırılabilir.
    /// </summary>
    private async Task<List<(string SourceId, string Path, string Content, bool HasEmbedding)>> StoredChunksAsync()
    {
        await using var context = CreateContext();
        var documents = await new KnowledgeDocumentRepository(context).ListWithChunksAsync(TestContext.Current.CancellationToken);
        return documents
            .SelectMany(document => document.Chunks.OrderBy(chunk => chunk.Order)
                .Select(chunk => (document.SourceId, chunk.SectionPath, chunk.Content, chunk.Embedding is not null)))
            .OrderBy(chunk => chunk.SourceId)
            .ToList();
    }

    /// <summary>
    /// İlk ingestion'da her bölümün ayrı bir chunk olarak içeriğiyle birlikte kaydedildiğini, her chunk'ın embed edildiğini
    /// ve indeksin hibrit modda (BM25 + vektör) kurulduğunu doğrular: 2 doküman eklenir, 3 chunk oluşur, 3'ü de embed
    /// edilir ve indekste 3 chunk bulunur.
    /// </summary>
    /// <remarks>
    /// Kayıtlı chunk'ların veritabanından ayrıca okunması, özetteki sayaçların kalıcı veriyle gerçekten örtüştüğünü kanıtlar;
    /// yalnızca sayaçlara bakan bir test, kaydedilmeyen ya da bölümleri karışan bir ingestion'ı fark etmezdi.
    /// </remarks>
    [Fact]
    public async Task First_ingestion_stores_every_section_embeds_it_and_builds_a_hybrid_index()
    {
        _source.Documents = [ReturnPolicy, Shipping];

        var result = await IngestAsync();

        result.Success.ShouldBeTrue();
        result.Data!.Added.ShouldBe(2);
        result.Data.Chunks.ShouldBe(3);
        result.Data.EmbeddedChunks.ShouldBe(3);
        result.Data.RetrievalMode.ShouldBe("hybrid");
        (await StoredChunksAsync()).ShouldBe(
        [
            ("iade", "İade Süresi", "30 gün içinde iade edebilirsiniz.", true),
            ("iade", "Para İadesi", "Ücret 5 iş günü içinde iade edilir.", true),
            ("kargo", "Kargo Ücreti", "750 TL üzeri ücretsiz.", true)
        ]);
        _index.Status.ChunkCount.ShouldBe(3);
    }

    /// <summary>
    /// İçerik özeti değişmemiş dokümanlarla ikinci kez ingestion yapıldığında hiçbir chunk'ın yeniden embed edilmediğini
    /// (<c>EmbeddedChunks = 0</c>), iki dokümanın da "değişmedi" sayıldığını ve indeksin yine hibrit modda kaldığını
    /// doğrular.
    /// </summary>
    /// <remarks>
    /// Embedding sunucusu uzak ve yavaştır; her açılışta ya da reindex'te tüm bilgi tabanını yeniden embed etmek gereksiz
    /// gecikme ve yük yaratır. Hibrit modun korunması, vektörlerin veritabanına (BLOB olarak) yazılıp yeni bir context ile
    /// geri okunduktan sonra da aynı model adıyla kullanılabilir kaldığını gösterir.
    /// </remarks>
    [Fact]
    public async Task Re_ingesting_unchanged_documents_reuses_the_stored_embeddings()
    {
        _source.Documents = [ReturnPolicy, Shipping];
        await IngestAsync();

        var result = await IngestAsync();

        result.Data!.Unchanged.ShouldBe(2);
        result.Data.Added.ShouldBe(0);
        result.Data.EmbeddedChunks.ShouldBe(0);
        result.Data.RetrievalMode.ShouldBe("hybrid");
    }

    /// <summary>
    /// Yalnızca kargo dokümanının içerik özeti değiştiğinde bu dokümanın yeniden bölünüp yeniden embed edildiğini (tek
    /// chunk), iade dokümanının ise dokunulmadan kaldığını doğrular; kayıtlı kargo chunk'ı yeni içeriği ve bir embedding'i
    /// taşır.
    /// </summary>
    /// <remarks>
    /// Bu yol geliştirme sırasında bulunan bir hatayı da korur: kimlikler domain'de atandığından, <c>ValueGeneratedNever</c>
    /// yapılandırması olmadan EF izlenen dokümanın koleksiyonuna eklenen yeni chunk'ı mevcut bir satır sanıp INSERT yerine
    /// UPDATE üretiyor ve <c>DbUpdateConcurrencyException</c> fırlatıyordu.
    /// </remarks>
    [Fact]
    public async Task A_changed_document_is_re_chunked_and_re_embedded_while_the_others_are_kept()
    {
        _source.Documents = [ReturnPolicy, Shipping];
        await IngestAsync();
        _source.Documents = [ReturnPolicy, StubKnowledgeBaseSource.Document("kargo", "hash-kargo-2", new SourceSection("Kargo Ücreti", "1000 TL üzeri ücretsiz."))];

        var result = await IngestAsync();

        result.Data!.Updated.ShouldBe(1);
        result.Data.Unchanged.ShouldBe(1);
        result.Data.EmbeddedChunks.ShouldBe(1);
        (await StoredChunksAsync()).Where(chunk => chunk.SourceId == "kargo")
            .ShouldBe([("kargo", "Kargo Ücreti", "1000 TL üzeri ücretsiz.", true)]);
    }

    /// <summary>
    /// Kaynaktan kaybolan bir dokümanın (kargo) veritabanından ve indeksten kaldırıldığını doğrular: özet
    /// <c>Removed = 1</c> bildirir, kayıtlı chunk'ların hepsi iade dokümanına aittir ve indekste tek doküman kalır.
    /// </summary>
    /// <remarks>
    /// Veritabanı dosyalardan türetilmiş veridir; silinen bir dosya kaldırılmazsa asistan artık var olmayan bir dokümana
    /// dayanarak yanıt vermeyi sürdürürdü.
    /// </remarks>
    [Fact]
    public async Task Documents_that_disappear_from_the_source_are_removed()
    {
        _source.Documents = [ReturnPolicy, Shipping];
        await IngestAsync();
        _source.Documents = [ReturnPolicy];

        var result = await IngestAsync();

        result.Data!.Removed.ShouldBe(1);
        (await StoredChunksAsync()).ShouldAllBe(chunk => chunk.SourceId == "iade");
        _index.Status.DocumentCount.ShouldBe(1);
    }

    /// <summary>
    /// Embedding sunucusuna ulaşılamadığında (bağlantı reddi) ya da sunucu yanıt vermeyip istek zaman aşımına uğradığında
    /// ingestion'ın yine başarılı olduğunu, hiçbir chunk'ın embed edilmediğini, indeksin lexical (yalnızca BM25) modda
    /// hazır olduğunu ve özetin bir uyarı (<c>EmbeddingUnavailable</c>) taşıdığını doğrular.
    /// </summary>
    /// <remarks>
    /// Embedding isteğe bağlı bir iyileştirmedir: sunucu çöktüğünde asistanın tümüyle kullanılamaz hâle gelmesi yerine
    /// arama BM25'e düşer ve sonraki bir reindex eksik vektörleri tamamlar. Uyarı, operatörün bu kademeli düşüşü fark
    /// etmesini sağlar. Zaman aşımı (<c>TaskCanceledException</c>) vakası bir hatanın regresyon testidir: eski filtre onu
    /// çağıranın iptali sanıyor ve asılı kalan bir sunucu açılıştaki indekslemeyi tamamen başarısız kılıyordu.
    /// </remarks>
    [Theory]
    [InlineData("down")]
    [InlineData("timeout")]
    public async Task An_unreachable_embedding_server_leaves_a_working_lexical_index(string failure)
    {
        _source.Documents = [ReturnPolicy, Shipping];
        _embedder.Failure = failure == "down"
            ? new HttpRequestException("connection refused")
            : new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");

        var result = await IngestAsync();

        result.Success.ShouldBeTrue();
        result.Data!.EmbeddedChunks.ShouldBe(0);
        result.Data.RetrievalMode.ShouldBe("lexical");
        result.Data.Warning.ShouldNotBeNullOrWhiteSpace();
        _index.IsReady.ShouldBeTrue();
    }

    /// <summary>
    /// Biçimi bozuk bir bilgi tabanı dosyasının (<c>KnowledgeBaseFormatException</c>) 422 ile raporlandığını, mesajın
    /// sorunlu dosyanın adını içerdiğini ve indeksin hazır hâle gelmediğini doğrular.
    /// </summary>
    /// <remarks>
    /// Biçim hatası mesajı yalnızca dosya adını ve eksik alanı içerir; operatör dosyayı düzeltebilsin diye istemciye aynen
    /// iletilir. Handler veritabanına ve indekse dokunmadan döndüğü için bozuk bir dosya yüzünden yarım bir bilgi tabanıyla
    /// arama yapılmaz.
    /// </remarks>
    [Fact]
    public async Task A_malformed_knowledge_base_is_reported_as_unprocessable()
    {
        _source.Failure = new KnowledgeBaseFormatException("bozuk.md: zorunlu front matter alanı eksik: title");

        var result = await IngestAsync();

        result.Success.ShouldBeFalse();
        result.StatusCode.ShouldBe(422);
        result.Message.ShouldContain("bozuk.md");
        _index.IsReady.ShouldBeFalse();
    }

    /// <summary>
    /// Dosya okuma hatalarının (<c>IOException</c> ve <c>UnauthorizedAccessException</c>) 422 ve genel
    /// <c>KnowledgeBaseUnreadable</c> mesajıyla raporlandığını doğrular. İstisna örnekleri öznitelik argümanı olamayacağı
    /// için <c>[InlineData]</c> yalnızca hangi istisnanın üretileceğini seçen bir anahtar ("io"/"access") taşır.
    /// </summary>
    /// <remarks>
    /// Bu istisnaların mesajları sunucudaki dosya yollarını ve işletim sistemi ayrıntılarını içerebilir; bu yüzden
    /// ayrıntılar yalnızca sunucu loglarına yazılır ve istemciye sabit bir mesaj döner. Test kırılırsa bu ayrıntılar API
    /// yanıtına sızabilir ya da hata işlenmeden 500'e dönüşebilir.
    /// </remarks>
    [Theory]
    [InlineData("io")]
    [InlineData("access")]
    public async Task An_unreadable_knowledge_base_file_is_reported_as_unprocessable(string failure)
    {
        _source.Failure = failure == "io" ? new IOException("disk error") : new UnauthorizedAccessException("denied");

        var result = await IngestAsync();

        result.Success.ShouldBeFalse();
        result.StatusCode.ShouldBe(422);
        result.Message.ShouldBe(Shared.Application.Common.Messages.Knowledge.KnowledgeBaseUnreadable);
    }

    /// <summary>
    /// Aynı doküman kimliğini (<c>iade</c>) kullanan iki dosyanın 422 ile reddedildiğini ve mesajın çakışan kimliği
    /// içerdiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Kimlik uzlaştırmanın anahtarıdır ve veritabanında benzersizdir; iki dosya aynı kimliği taşırsa hangisinin doğru
    /// olduğu belirsizleşir ve kayıt sırasında benzersizlik ihlali oluşur. Erken ve açıklayıcı bir ret, operatöre hangi
    /// kimliği düzeltmesi gerektiğini söyler.
    /// </remarks>
    [Fact]
    public async Task Duplicate_document_ids_are_rejected()
    {
        _source.Documents = [ReturnPolicy, StubKnowledgeBaseSource.Document("iade", "hash-other", new SourceSection("A", "B"))];

        var result = await IngestAsync();

        result.Success.ShouldBeFalse();
        result.StatusCode.ShouldBe(422);
        result.Message.ShouldContain("iade");
    }

    /// <summary>
    /// Hiç doküman içermeyen bir bilgi tabanının 422 ile reddedildiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Bu kontrol olmasaydı boş bir kaynak (ör. yanlış klasör) "bütün dokümanlar silindi" diye yorumlanır, kayıtlı
    /// dokümanların hepsi kaldırılır ve boş bir indeks kurulurdu.
    /// </remarks>
    [Fact]
    public async Task An_empty_knowledge_base_is_rejected()
    {
        _source.Documents = [];

        var result = await IngestAsync();

        result.Success.ShouldBeFalse();
        result.StatusCode.ShouldBe(422);
    }
}
