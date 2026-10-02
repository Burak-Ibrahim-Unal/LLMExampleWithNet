using System.Net;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

/// <summary>
/// <c>GET /v1/health</c> ucunun sağlıklı test kurulumunda indeks, dil modeli ve embedding durumunu doğru raporladığını
/// doğrular. Host, <see cref="SupportAssistantApiFactory"/> sınıf fikstürüyle bu sınıfın testleri arasında paylaşılır.
/// </summary>
public sealed class HealthEndpointTests(SupportAssistantApiFactory factory) : IClassFixture<SupportAssistantApiFactory>
{
    /// <summary>
    /// Sağlık ucunun 200 ve <c>ApiResult</c> zarfı içinde <c>status = ok</c> döndürdüğünü; indeksin hazır olduğunu, üç
    /// fikstür dokümanını saydığını, embedding kapalı olduğu için arama modunun <c>lexical</c> (yalnızca BM25) olduğunu ve
    /// dil modelinin yapılandırılmış göründüğünü doğrular.
    /// </summary>
    /// <remarks>
    /// Sağlık ucu, operatörün ve değerlendirme aracının sistemin hangi modda çalıştığını gördüğü yerdir; değerlendirme
    /// raporunun başlığındaki model ve arama modu bilgisi de buradan okunur. Bu alanlar yanlış raporlanırsa (ör. embedding
    /// sunucusu yokken <c>hybrid</c> görünürse) ölçülen sonuçlar yanlış yapılandırmaya atfedilir.
    /// </remarks>
    [Fact]
    public async Task Health_reports_the_index_and_model_status_in_the_api_result_envelope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await ApiResponse.ReadAsync(await client.GetAsync("/v1/health", cancellationToken), cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Success.ShouldBeTrue();
        response.Data.GetProperty("status").GetString().ShouldBe("ok");
        var index = response.Data.GetProperty("index");
        index.GetProperty("ready").GetBoolean().ShouldBeTrue();
        index.GetProperty("documents").GetInt32().ShouldBe(3);
        index.GetProperty("retrievalMode").GetString().ShouldBe("lexical");
        response.Data.GetProperty("llm").GetProperty("configured").GetBoolean().ShouldBeTrue();
        response.Data.GetProperty("embeddings").GetProperty("configured").GetBoolean().ShouldBeFalse();
    }
}

/// <summary>
/// Bilgi tabanı okunamadığında sağlık ucunun sorunu gizlemeden <c>degraded</c> raporladığını doğrular
/// (<see cref="MissingKnowledgeBaseApiFactory"/> ile, yani bilgi tabanı klasörü bulunamazken).
/// </summary>
public sealed class DegradedHealthTests(MissingKnowledgeBaseApiFactory factory) : IClassFixture<MissingKnowledgeBaseApiFactory>
{
    /// <summary>
    /// İndeks kurulamamışken sağlık ucunun <c>status = degraded</c> ve <c>index.ready = false</c> döndürdüğünü doğrular.
    /// </summary>
    /// <remarks>
    /// Açılış indekslemesi başarısız olduğunda uygulama çökmez, çalışmaya devam eder; bu durumda sorunun dışarıdan
    /// görülebildiği yer sağlık ucudur. Burada <c>ok</c> dönerse operatör ve izleme sistemi, her soruya 503 veren bir
    /// servisi sağlıklı sanır.
    /// </remarks>
    [Fact]
    public async Task Health_is_degraded_while_the_index_is_not_built()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await ApiResponse.ReadAsync(await client.GetAsync("/v1/health", cancellationToken), cancellationToken);

        response.Data.GetProperty("status").GetString().ShouldBe("degraded");
        response.Data.GetProperty("index").GetProperty("ready").GetBoolean().ShouldBeFalse();
    }
}
