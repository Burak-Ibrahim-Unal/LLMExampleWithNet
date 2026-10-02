using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace SupportAssistant.UnitTests.Architecture;

/// <summary>
/// <c>agent.md</c>'de tanımlanan katman kuralları; derlenmiş assembly'ler üzerinde NetArchTest ile zorunlu kılınır.
/// </summary>
/// <remarks>
/// Modüler monolit, bağımlılıkların yalnızca içe doğru akmasına dayanır: iç katmanlar (<c>Shared.Kernel</c>,
/// <c>Knowledge.Domain</c>, uygulama katmanları) dış katmanları (Infrastructure, API) ve veritabanı/LLM SDK'larını
/// tanımaz. Kod incelemesinde gözden kaçan tek bir <c>using</c> bu sınırı sessizce delebilir; bu testler ihlali, ihlal
/// eden tiplerin adlarıyla birlikte yakalar. Assembly'ler ad dizesiyle değil derleme zamanında çözülen bir tip üzerinden
/// alınır; bir proje yeniden adlandırılır ya da tip taşınırsa hata çalışma zamanında değil derlemede görünür. Altyapı
/// projeleri ve API host'u gibi dış katmanlar iç katmanlara bağımlı olabildiği için ayrı bir kural taşımaz; API'de
/// yalnızca endpoint'ler sınırlanır. Kurala aykırı bir tasarım gerekiyorsa önce mimari karar yazılır, sonra test bilinçli
/// olarak güncellenir.
/// </remarks>
public sealed class ArchitectureTests
{
    /// <summary>En içteki paylaşılan çekirdek (<c>EntityBase</c>, <c>IRepository</c>).</summary>
    private static readonly Assembly SharedKernel = typeof(Shared.Kernel.Abstractions.EntityBase).Assembly;
    /// <summary>Paylaşılan uygulama katmanı (<c>ApiResult&lt;T&gt;</c>, <c>Messages</c>, migrator/seeder soyutlamaları).</summary>
    private static readonly Assembly SharedApplication = typeof(Shared.Application.Common.ApiResult<>).Assembly;
    /// <summary>Knowledge modülünün domain katmanı (varlıklar ve repository arayüzleri).</summary>
    private static readonly Assembly KnowledgeDomain = typeof(Knowledge.Domain.Entities.KnowledgeDocument).Assembly;
    /// <summary>Knowledge modülünün uygulama katmanı; projenin kendi <c>AssemblyReference</c> işaretçisiyle alınır.</summary>
    private static readonly Assembly KnowledgeApplication = Knowledge.Application.AssemblyReference.Assembly;
    /// <summary>API host'u; <c>public partial class Program</c> bildirimi sayesinde test projesinden erişilebilir.</summary>
    private static readonly Assembly Api = typeof(Program).Assembly;

    /// <summary>
    /// NetArchTest sonucunu doğrular; başarısızlıkta kuralı ihlal eden tiplerin adlarını hata mesajına ekler. Yalnızca
    /// <c>IsSuccessful</c> değerine bakmak "false" dışında bilgi vermezdi; tip adları ihlalin yerini doğrudan gösterir.
    /// </summary>
    private static void ShouldPass(NetArchTest.Rules.TestResult result)
    {
        result.IsSuccessful.ShouldBeTrue($"Violating types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>
    /// <c>Shared.Kernel</c>'in EF Core'a, <c>Microsoft.Extensions.*</c> paketlerine, <c>Shared.Application</c>'a ve
    /// <c>Shared.Infrastructure</c>'a bağımlı olmadığını doğrular. Çekirdek (<c>EntityBase</c>, <c>IRepository</c>) her
    /// modülün domain'i tarafından kullanılır; buraya giren bir framework bağımlılığı tüm domain'lere sızar ve varlıkları
    /// kalıcılık teknolojisine bağlar. Dış katmanlara bağımlılık ise katmanlar arasında döngü yaratır.
    /// </summary>
    [Fact]
    public void Shared_kernel_has_no_framework_or_outer_layer_dependencies()
    {
        ShouldPass(Types.InAssembly(SharedKernel).ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.Extensions", "Shared.Application", "Shared.Infrastructure")
            .GetResult());
    }

    /// <summary>
    /// <c>Shared.Application</c>'ın <c>Shared.Infrastructure</c>'a ve EF Core'a bağımlı olmadığını doğrular.
    /// <c>IDatabaseMigrator</c> ve <c>IDatabaseSeeder</c> soyutlamaları tam da bu yüzden burada durur: uygulamanın geri
    /// kalanı EF'i bilmeden migrasyon ve seed isteyebilir, EF kullanan gerçeklemeler (<c>DbMigrator</c>,
    /// <c>DbSeeder</c>) altyapıda kalır (bağımlılığın tersine çevrilmesi).
    /// </summary>
    [Fact]
    public void Shared_application_does_not_depend_on_infrastructure()
    {
        ShouldPass(Types.InAssembly(SharedApplication).ShouldNot()
            .HaveDependencyOnAny("Shared.Infrastructure", "Microsoft.EntityFrameworkCore")
            .GetResult());
    }

    /// <summary>
    /// <c>Knowledge.Domain</c>'in uygulama ve altyapı katmanlarına, EF Core'a ve MediatR'a bağımlı olmadığını doğrular.
    /// </summary>
    /// <remarks>
    /// Varlıklar (<c>KnowledgeDocument</c>, <c>DocumentChunk</c>) saf kalmalıdır: EF eşlemeleri altyapıdaki
    /// <c>IEntityTypeConfiguration</c> sınıflarında, akış ise uygulama katmanındaki MediatR handler'larındadır. Böylece
    /// domain kuralları framework'süz test edilebilir ve kalıcılık teknolojisi domain'e dokunmadan değişebilir. Kural bir
    /// yasak listesidir; projenin yalnızca <c>Shared.Kernel</c>'e referans vermesiyle birlikte test adındaki "yalnızca
    /// çekirdeğe bağımlı" garantisini sağlar.
    /// </remarks>
    [Fact]
    public void Knowledge_domain_depends_only_on_the_kernel()
    {
        ShouldPass(Types.InAssembly(KnowledgeDomain).ShouldNot()
            .HaveDependencyOnAny("Knowledge.Application", "Knowledge.Infrastructure", "Microsoft.EntityFrameworkCore", "MediatR")
            .GetResult());
    }

    /// <summary>
    /// <c>Knowledge.Application</c>'ın altyapı katmanlarına, EF Core'a, <c>OpenAI</c> SDK'sına ve
    /// <c>Microsoft.Extensions.AI</c>'a bağımlı olmadığını doğrular.
    /// </summary>
    /// <remarks>
    /// Cevaplama mantığı (kapılar, <c>VersionResolver</c>, <c>CitationValidator</c>) yalnızca kendi portlarıyla
    /// (<c>ITextEmbedder</c>, <c>IGroundedAnswerGenerator</c>, <c>IKnowledgeIndex</c>, <c>IKnowledgeBaseSource</c>)
    /// konuşur. Bu sayede testler gerçek LLM ya da embedding sunucusu olmadan sahte nesnelerle çalışır ve sağlayıcı
    /// (llama.cpp, LM Studio, OpenAI…) yalnızca yapılandırmayla değiştirilebilir. SDK tipleri buraya sızsaydı iş kuralları
    /// belirli bir sağlayıcıya ve veritabanına kilitlenirdi.
    /// </remarks>
    [Fact]
    public void Knowledge_application_does_not_know_the_database_or_the_llm_sdks()
    {
        ShouldPass(Types.InAssembly(KnowledgeApplication).ShouldNot()
            .HaveDependencyOnAny("Knowledge.Infrastructure", "Shared.Infrastructure", "Microsoft.EntityFrameworkCore", "OpenAI", "Microsoft.Extensions.AI")
            .GetResult());
    }

    /// <summary>
    /// <c>SupportAssistant.API.Endpoints</c> altındaki endpoint'lerin altyapı katmanlarına, domain repository arayüzlerine
    /// ve EF Core'a doğrudan bağımlı olmadığını doğrular.
    /// </summary>
    /// <remarks>
    /// Endpoint'ler ince adaptörlerdir: isteği bağlar, <c>IKnowledgeService</c>'i çağırır ve <c>ApiResult</c> zarfını
    /// kendi durum koduyla döndürür. Bir endpoint repository'ye ya da <c>DbContext</c>'e doğrudan erişseydi iş
    /// kurallarını (<c>KnowledgeBusinessRules</c>), MediatR akışını ve tek tip yanıt zarfını atlardı. Kural yalnızca
    /// endpoint ad alanına uygulanır; <c>Program</c> ve DI uzantıları bileşim kökü (composition root) olarak altyapıyı
    /// bilmek zorundadır.
    /// </remarks>
    [Fact]
    public void Api_endpoints_go_through_the_service_layer()
    {
        ShouldPass(Types.InAssembly(Api).That().ResideInNamespace("SupportAssistant.API.Endpoints").ShouldNot()
            .HaveDependencyOnAny("Shared.Infrastructure", "Knowledge.Infrastructure", "Knowledge.Domain.Repositories", "Microsoft.EntityFrameworkCore")
            .GetResult());
    }
}
