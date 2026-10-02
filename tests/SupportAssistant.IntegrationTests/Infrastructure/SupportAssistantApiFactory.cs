using Knowledge.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SupportAssistant.IntegrationTests.Infrastructure;

/// <summary>
/// Gerçek API'yi test sürecinin içinde, ağ portu açmadan (bellek içi <c>TestServer</c> üzerinden) çalıştıran fabrika.
/// <c>Program.cs</c> olduğu gibi çalışır: DI kayıtları, FastEndpoints uçları, veritabanı hazırlığı ve açılış indekslemesi gerçektir;
/// yalnızca dış bağımlılıklar yalıtılır. Her fabrika örneği kendine ait, atılabilir bir SQLite dosyası ve küçük bir
/// fikstür bilgi tabanı (<c>TestData/knowledge-base</c>: <c>iade-v1</c>, <c>iade-v2</c>, <c>kargo</c>) kullanır.
/// </summary>
/// <remarks>
/// Entegrasyon testleri HTTP sözleşmesini (yollar, durum kodları, <c>ApiResult</c> zarfı) ve katmanların doğru
/// bağlandığını uçtan uca doğrular; gerçek bir dil modeli ya da embedding sunucusu gerektirmemeleri ve her makinede aynı
/// sonucu vermeleri gerekir. Bu yüzden embedding ve LLM uç adresleri boşaltılır (arama ağa çıkmadan, yalnızca BM25 ile
/// çalışır) ve dil modelinin yerine deterministik <see cref="FakeAnswerGenerator"/> konur. Fikstür, aynı doküman
/// ailesinin (<c>iade</c>) eski ve yürürlükteki sürümünü içerdiği için sürüm çözümü de gerçek akışın içinde sınanır.
/// Sınıf bilinçli olarak <c>sealed</c> değildir: hata senaryosu fabrikaları (eksik bilgi tabanı, açılışta hata veren
/// kaynak) bu kurulumu devralıp yalnızca farkı değiştirir.
/// </remarks>
public class SupportAssistantApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Bu fabrikaya özel geçici SQLite dosyası. Adındaki GUID sayesinde paralel çalışan test sınıfları (xunit her sınıfa
    /// kendi fikstür örneğini verir) aynı veritabanını paylaşmaz ve önceki koşulardan veri sızmaz. Bellek içi
    /// (<c>:memory:</c>) SQLite yerine dosya kullanılır: bellek içi veritabanı yalnızca onu açan bağlantı açık kaldıkça
    /// yaşar, EF Core ise bağlantıyı işlem başına açıp kapatır. Dosya ayrıca üretimdeki şema oluşturma yolundan
    /// (<c>DbMigrator</c>) aynen geçer.
    /// </summary>
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"supportassistant-tests-{Guid.NewGuid():N}.db");

    /// <summary>
    /// Açılış indekslemesinin okuyacağı bilgi tabanı klasörü; varsayılan olarak test projesinin çıktı klasörüne kopyalanan
    /// fikstür dokümanları. Mutlak yol verildiği için <c>MarkdownKnowledgeSource</c> üst klasörlerde arama yapmaz; göreli
    /// bir yol yukarı doğru aranır ve yanlış bir klasör (ör. depo kökündeki gerçek <c>knowledge-base/</c>) bulunabilirdi.
    /// <c>virtual</c> olması, türetilmiş fabrikaların yalnızca bu yolu değiştirerek hata senaryosu kurmasını sağlar.
    /// </summary>
    protected virtual string KnowledgeBasePath => Path.Combine(AppContext.BaseDirectory, "TestData", "knowledge-base");

    /// <summary>
    /// Test host'unu yalıtır: ortamı <c>Testing</c> yapar; bağlantı dizesini, bilgi tabanı yolunu ve dış uç adreslerini
    /// bellek içi yapılandırmayla ezer; dil modelini <see cref="FakeAnswerGenerator"/> ile değiştirir.
    /// </summary>
    /// <remarks>
    /// Bellek içi kaynak en son eklenen yapılandırma kaynağı olduğu için <c>appsettings.json</c>'ı ve ortam değişkenlerini
    /// ezer. Geliştiricinin <c>.env</c> dosyası (uzak model uç adresleri) <c>Program.cs</c>'te yalnızca süreç ortam
    /// değişkeni <c>ASPNETCORE_ENVIRONMENT</c> "Development" ise yüklenir; normal bir test koşusunda bu değişken tanımlı
    /// değildir, tanımlı olsa bile buradaki boş uç adresleri <c>.env</c>'den gelen adresleri ezer. Bu değerlerin etkili
    /// olması, uygulamanın yapılandırmayı servis kaydı sırasında değil ilk kullanımda okumasına bağlıdır: seçenekler
    /// (<c>IOptions</c>), embedder/LLM fabrikaları ve bağlantı dizesi tembel çözülür.
    /// </remarks>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // WebApplicationFactory host'u varsayılan olarak Development ortamında başlatır. Testing bu varsayılanı ezer;
        // appsettings.Development.json gibi yalnızca geliştirmeye özgü ayar ve davranışlar testlere karışmaz.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}",
                ["KnowledgeBase:Path"] = KnowledgeBasePath,
                // Boş adresler ağ erişimini keser: embedding kapanır (DisabledTextEmbedder, yalnızca BM25) ve gerçek LLM
                // istemcisi hiç kurulmaz; sahte üretici devreye girmese bile testler uzak bir sunucuya istek atamaz.
                ["Embeddings:BaseUrl"] = string.Empty,
                ["Llm:BaseUrl"] = string.Empty
            });
        });

        // ConfigureTestServices uygulamanın kendi kayıtlarından sonra çalışır; DI tek bir servisi çözerken son kaydı
        // kullandığı için sahte üretici gerçek IGroundedAnswerGenerator kaydının yerini alır.
        builder.ConfigureTestServices(services => services.AddSingleton<IGroundedAnswerGenerator, FakeAnswerGenerator>());
    }

    /// <summary>
    /// Host'u kapatır ve bu fabrikaya ait geçici veritabanı dosyasını siler; testler geçici klasörde dosya biriktirmez.
    /// Önce <c>base.DisposeAsync()</c> çağrılır ki host ve DbContext'ler bağlantılarını bırakmış olsun.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        // Microsoft.Data.Sqlite bağlantıları havuzda tutar ve havuzdaki bağlantı dosyayı açık bırakır; havuz boşaltılmadan
        // silmek (özellikle Windows'ta) "dosya kullanımda" hatası verir.
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}

/// <summary>
/// API'yi var olmayan bir bilgi tabanı klasörüne yönlendirir; açılış indekslemesi "klasör bulunamadı" hatasıyla başarısız
/// olur ve indeks hiç kurulmaz.
/// </summary>
/// <remarks>
/// Bu, ingestion handler'ının yakalayıp 422 sonucuna çevirdiği (istisna fırlatmayan) hata yoludur; <c>Program.cs</c>
/// sonucu loglar ve host yine ayağa kalkar. Sağlık ucunun <c>degraded</c>, aramanın 503 ve yeniden indekslemenin
/// açıklayıcı bir 422 döndürdüğünü doğrulayan testler bu fabrikayı kullanır.
/// </remarks>
public sealed class MissingKnowledgeBaseApiFactory : SupportAssistantApiFactory
{
    /// <summary>
    /// Var olmayan, mutlak bir yol; üst klasörlerde arama yapılmadığı için başka bir klasör yanlışlıkla bulunamaz.
    /// </summary>
    protected override string KnowledgeBasePath => Path.Combine(AppContext.BaseDirectory, "TestData", "does-not-exist");
}
