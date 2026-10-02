using System.Net;
using Knowledge.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

/// <summary>
/// Okunurken beklenmedik biçimde hata veren bir bilgi tabanı tüm API'yi çökertmemelidir; bu fabrika API'yi, her okumada
/// istisna fırlatan bir bilgi tabanı kaynağıyla başlatır.
/// </summary>
/// <remarks>
/// <see cref="MissingKnowledgeBaseApiFactory"/>'den farkı: oradaki hata ingestion handler'ında yakalanıp 422 sonucuna
/// çevrilir. Buradaki <see cref="InvalidOperationException"/> ise handler'ın yakaladığı türlerden
/// (<c>KnowledgeBaseFormatException</c>, <c>IOException</c>, <c>UnauthorizedAccessException</c>) değildir ve
/// <c>Program.cs</c>'teki açılış try/catch bloğuna kadar yükselir. İki fabrika birlikte açılış indekslemesinin iki hata
/// yolunu da kapsar.
/// </remarks>
public sealed class FailingKnowledgeBaseApiFactory : SupportAssistantApiFactory
{
    /// <summary>
    /// <c>LoadAsync</c> her çağrıldığında <see cref="InvalidOperationException"/> fırlatan test dublörü. Gerçek dosya
    /// sisteminde güvenilir biçimde üretilemeyen "beklenmedik hata" durumunu taklit eder; açılış indekslemesi hiçbir zaman
    /// başarılı olamaz.
    /// </summary>
    private sealed class FailingSource : IKnowledgeBaseSource
    {
        /// <summary>Hiçbir doküman döndürmeden, handler'ın yakalamadığı bir istisna fırlatır.</summary>
        public Task<IReadOnlyList<SourceDocument>> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected failure while reading the knowledge base");
    }

    /// <summary>
    /// Temel kurulumu (Testing ortamı, geçici veritabanı, sahte model) aynen uygular, ardından bilgi tabanı kaynağını
    /// <c>FailingSource</c> ile değiştirir. Kayıt <c>base</c> çağrısından sonra yapıldığı için DI'daki son kayıt olur ve
    /// gerçek <c>MarkdownKnowledgeSource</c>'un yerini alır.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IKnowledgeBaseSource, FailingSource>());
    }
}

/// <summary>
/// Açılış dayanıklılığı testleri: açılıştaki indeksleme beklenmedik bir istisnayla başarısız olsa bile host'un ayağa
/// kalktığını ve durumu dürüstçe raporladığını doğrular (<see cref="FailingKnowledgeBaseApiFactory"/> ile).
/// </summary>
public sealed class StartupResilienceTests(FailingKnowledgeBaseApiFactory factory) : IClassFixture<FailingKnowledgeBaseApiFactory>
{
    /// <summary>
    /// Açılış indekslemesi istisna fırlattığında API'nin yine de başladığını; sağlık ucunun 200 ile
    /// <c>status = degraded</c> ve <c>index.ready = false</c> döndürdüğünü doğrular.
    /// </summary>
    /// <remarks>
    /// <c>Program.cs</c>'teki try/catch kaldırılırsa istisna giriş noktasından dışarı çıkar, host hiç başlamaz ve
    /// <c>CreateClient</c> çağrısı hata verir. Bozuk bir bilgi tabanı tüm servisi düşürmemelidir: sağlık ucu sorunu
    /// raporlamaya devam eder ve dosyalar düzeltildikten sonra <c>POST /v1/documents/reindex</c> ile yeniden denenebilir.
    /// </remarks>
    [Fact]
    public async Task The_api_starts_and_reports_degraded_health_when_indexing_fails_at_startup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await ApiResponse.ReadAsync(await client.GetAsync("/v1/health", cancellationToken), cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("status").GetString().ShouldBe("degraded");
        response.Data.GetProperty("index").GetProperty("ready").GetBoolean().ShouldBeFalse();
    }
}
