using Knowledge.Application.Abstractions;

namespace SupportAssistant.UnitTests.TestDoubles;

/// <summary>
/// Dil modelinin yerine geçer (<see cref="IGroundedAnswerGenerator"/> portunun sahte uygulaması). Varsayılan olarak aldığı
/// ilk kaynağı alıntılayarak yanıt verir.
/// </summary>
/// <remarks>
/// Yanıt hattı testleri böylece gerçek bir LLM sunucusu olmadan, hızlı ve her çalıştırmada aynı sonuçla koşar. Fake hem
/// model davranışını yönlendirmeye (<see cref="Respond"/>, <see cref="Failure"/>) hem de modelin çağrılıp çağrılmadığını
/// ve neyi gördüğünü gözlemlemeye (<see cref="Calls"/>, <see cref="LastContext"/>) olanak verir.
/// </remarks>
internal sealed class FakeAnswerGenerator : IGroundedAnswerGenerator
{
    /// <summary>
    /// Her zaman <c>true</c>: model yapılandırılmış gibi davranılır. Gerçek uygulamada bu değer sağlık durumunu
    /// (<c>GET /v1/health</c>) belirler; <c>Llm:BaseUrl</c> boşken yerine <c>UnconfiguredAnswerGenerator</c> kullanılır.
    /// </summary>
    public bool IsConfigured => true;

    /// <summary>
    /// Sağlık durumunda raporlanan sabit model adı; <see cref="Answer"/> ve <see cref="NotAnswerable"/> de aynı adı
    /// yanıtın <c>Model</c> alanına yazar, böylece tanılama (diagnostics) tutarlı bir ad gösterir.
    /// </summary>
    public string ModelName => "fake-llm";

    /// <summary>
    /// <see cref="GenerateAsync"/> çağrı sayısı. Kapı 1'in altında kalan ya da yürürlükte kaynağı olmayan sorularda modelin
    /// hiç çağrılmadığını (<c>0</c>), Kapı 2 testinde ise tam bir kez çağrıldığını doğrulamak için kullanılır.
    /// </summary>
    public int Calls { get; private set; }

    /// <summary>
    /// Son çağrıda modele verilen bağlam (C1..Cn etiketli bölümler). Eski sürümlerin modele hiç ulaşmadığını doğrulayan
    /// test buna bakar.
    /// </summary>
    public IReadOnlyList<ContextChunk> LastContext { get; private set; } = [];

    /// <summary>
    /// Sorudan ve bağlamdan modelin "yanıtını" üreten temsilci; varsayılanı <see cref="QuoteFirstSource"/>. Testler bunu
    /// değiştirerek ret (<see cref="NotAnswerable"/>), verilmemiş bir kaynağa atıf, çelişki raporu ya da yalnızca kaynak
    /// işaretinden oluşan yanıt gibi model davranışlarını taklit eder.
    /// </summary>
    public Func<string, IReadOnlyList<ContextChunk>, GeneratedAnswer> Respond { get; set; } = QuoteFirstSource;

    /// <summary>
    /// Doluysa <see cref="GenerateAsync"/> bu istisnayla başarısız olur. <c>AnswerGenerationException</c> vererek modele
    /// ulaşılamaması (503) ve geçersiz çıktı (502) durumlarının doğru durum kodlarına eşlendiğini sınamak için kullanılır.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// Çağrıyı ve bağlamı kaydeder; ardından <see cref="Failure"/> doluysa hata veren bir görev, değilse
    /// <see cref="Respond"/> sonucunu döndürür. Kayıt hatadan önce yapılır, böylece başarısız çağrılar da sayılır. İstisna
    /// senkron fırlatılmak yerine görevin içinde döner; gerçek asenkron üreticide olduğu gibi <c>await</c> sırasında
    /// ortaya çıkar.
    /// </summary>
    public Task<GeneratedAnswer> GenerateAsync(string question, IReadOnlyList<ContextChunk> context, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastContext = context;

        return Failure is not null ? Task.FromException<GeneratedAnswer>(Failure) : Task.FromResult(Respond(question, context));
    }

    /// <summary>
    /// Varsayılan "mutlu yol" yanıtı: ilk kaynağın içeriğini yanıt olarak verir ve aynı metni birebir alıntılayarak ona atıf
    /// yapar. Atıf geçerli ve doğrulanmış olduğundan yanıt bütün kapılardan geçer.
    /// </summary>
    public static GeneratedAnswer QuoteFirstSource(string question, IReadOnlyList<ContextChunk> context) =>
        Answer(context[0].Chunk.Content, new GeneratedCitation(context[0].Label, context[0].Chunk.Content));

    /// <summary>
    /// Verilen metin ve atıflarla yanıtlanabilir (<c>answerable=true</c>) bir model çıktısı oluşturur. Model adı ve token
    /// sayıları (10/5) sabit ve anlamsız değerlerdir; yalnızca tanılama alanlarını doldurmak içindir.
    /// </summary>
    public static GeneratedAnswer Answer(string text, params GeneratedCitation[] citations) =>
        new(true, text, citations, string.Empty, [], "fake-llm", 10, 5);

    /// <summary>
    /// Modelin kaynakları yetersiz bulduğu (<c>answerable=false</c>) bir çıktı oluşturur; Kapı 2 retlerini ve eksik bilgi
    /// açıklamasının <c>missingInformation</c> alanına taşınmasını sınamak için kullanılır.
    /// </summary>
    public static GeneratedAnswer NotAnswerable(string missingInformation) =>
        new(false, string.Empty, [], missingInformation, [], "fake-llm", 10, 5);
}
