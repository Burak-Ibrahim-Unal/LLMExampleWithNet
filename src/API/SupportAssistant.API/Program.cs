using DotNetEnv;
using FastEndpoints;
using FastEndpoints.OpenApi;
using Knowledge.Service.Abstractions;
using Scalar.AspNetCore;
using Shared.Application.Abstractions;
using SupportAssistant.API.Extensions;

// Yerel geliştirme kolaylığı: git'e alınmayan .env dosyasındaki değerler (bkz. .env.example) ortam değişkenine
// dönüşür. Gerçek ortam değişkenleri her zaman kazanır (NoClobber var olan bir değişkenin üzerine yazmaz); Development
// dışındaki ortamlar yalnızca gerçek ortam değişkenlerini veya secret store'u kullanır. Böylece API anahtarları kaynak
// koda ve appsettings dosyalarına hiç girmez. Bu adım builder oluşturulmadan önce çalışmalıdır ki yapılandırma sistemi
// değerleri ortam değişkeni olarak görsün; ortam adı bu yüzden henüz IHostEnvironment yokken doğrudan
// ASPNETCORE_ENVIRONMENT'tan okunur. TraversePath dosyayı üst klasörlerde de arar; uygulama proje klasöründen
// başlatıldığında repo kökündeki .env bulunur.
if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
{
    Env.TraversePath().NoClobber().Load();
}

var builder = WebApplication.CreateBuilder(args);

// Servis kayıtları dört extension'a ayrılır: web katmanı (FastEndpoints, OpenAPI belgesi, MediatR), koruma katmanları
// (hız sınırı), modül servisleri (zaman kaynağı, repository'ler, iş kuralları, cevaplama politikaları,
// IKnowledgeService) ve altyapı (SQLite DbContext, Knowledge adaptörleri, migrator/seeder). Program.cs böylece yalnızca
// açılış akışını gösterir.
builder.Services.AddWebApiServices();
builder.Services.AddSecurityServices(builder.Configuration);
builder.Services.AddModuleServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Hız sınırı uç noktalardan önce çalışır; politika yalnızca onu isteyen uca (POST /v1/questions) uygulanır.
app.UseRateLimiter();

// Tüm uç noktalar "v1" önekiyle yayınlanır (ör. /v1/questions). Sözleşme ileride uyumsuz biçimde değişirse yeni bir
// sürüm, mevcut istemcileri bozmadan yanına eklenebilir.
app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "v1";
});

// OpenAPI belgesi (/openapi/v1.json) ve onu okuyan etkileşimli Scalar arayüzü (/scalar): uç noktalar, Türkçe özet ve
// açıklamalarıyla tarayıcıdan incelenip denenebilir.
app.MapOpenApi();
app.MapScalarApiReference();

// Açılış adımları sırayla: şema → (boş) tohum → bilgi tabanı ingestion'ı. DbContext ve repository'ler scoped olduğu için
// bir HTTP isteği dışında çözülebilmeleri adına açık bir DI scope'u oluşturulur.
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

    await migrator.MigrateAsync();
    await seeder.SeedAsync();

    // Arama indeksini knowledge-base/ klasöründen kurar; değişmemiş dokümanlar saklanan embedding'lerini korur, böylece
    // her açılışta uzak embedding sunucusuna yeniden gidilmez. Hata fırlatılmaz, loglanır: bozuk veya eksik bir bilgi
    // tabanı host'u çökertmemelidir. API yine ayağa kalkar; health "degraded" gösterir, soru ve arama istekleri başarılı
    // bir reindex'e (POST /v1/documents/reindex) kadar 503 döner.
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var ingestion = await scope.ServiceProvider.GetRequiredService<IKnowledgeService>().ReindexAsync();

        if (ingestion.Success)
        {
            logger.LogInformation(
                "Knowledge base indexed: {Documents} documents, {Chunks} sections, {RetrievalMode} retrieval.",
                ingestion.Data!.Documents,
                ingestion.Data.Chunks,
                ingestion.Data.RetrievalMode);

            // Embedding servisine ulaşılamadıysa ingestion yine başarılıdır; aramanın BM25'e düştüğü uyarı olarak loglanır.
            if (ingestion.Data.Warning is not null)
            {
                logger.LogWarning("{Warning}", ingestion.Data.Warning);
            }
        }
        else
        {
            // Beklenen hatalar (boş, okunamayan veya geçersiz bilgi tabanı, yinelenen doküman kimliği) exception olarak
            // değil, başarısız bir ApiResult olarak döner.
            logger.LogError("Knowledge base could not be indexed: {Message}", ingestion.Message);
        }
    }
    catch (Exception exception)
    {
        // Beklenmeyen hatalar da açılışı durdurmaz; bu davranışı StartupResilienceTests entegrasyon testi korur.
        logger.LogError(exception, "Knowledge base indexing failed at startup; questions return 503 until POST /v1/documents/reindex succeeds.");
    }
}

// Ingestion bu satırdan önce beklendiği için sunucu istek kabul etmeye başladığında indeks hazırdır (ya da hata
// loglanmış ve health "degraded" durumundadır).
app.Run();

/// <summary>
/// Top-level statements için derleyicinin ürettiği <c>Program</c> sınıfını public yapar. Entegrasyon testlerindeki
/// <c>WebApplicationFactory&lt;Program&gt;</c> uygulamanın giriş noktasını bu tip üzerinden bulur ve API'yi süreç içinde
/// ayağa kaldırır; mimari testleri de API derlemesine bu tip üzerinden erişir.
/// </summary>
public partial class Program;
