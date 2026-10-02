using Knowledge.Application.Contracts;

namespace SupportAssistant.Eval;

/// <summary>
/// <c>eval/questions.json</c> dosyasının kök nesnesi: normal, cevapsız ve çelişkili kategorilerdeki değerlendirme
/// soruları. Sorular kodda değil veri dosyasında tutulur; yeni bir soru eklemek derleme gerektirmez ve beklentiler
/// kod okumadan incelenebilir.
/// </summary>
public sealed record EvalSuite(IReadOnlyList<EvalQuestion> Questions);

/// <summary>
/// Değerlendirme setindeki tek bir soru: kimlik, kategori, soru metni, insan için referans yanıt ve makinece denetlenen
/// beklentiler.
/// </summary>
/// <param name="Id">Raporda ve konsolda görünen kısa kimlik (ör. <c>N01</c>, <c>U02</c>, <c>C03</c>).</param>
/// <param name="Category">
/// Kategori anahtarı: <c>normal</c> | <c>cevapsiz</c> | <c>celiskili</c> (dokümanlardan yanıtlanabilen, dokümanlarda
/// bilgisi olmayan ve sürümleri ya da kaynakları çelişen sorular). Rapordaki kategori özeti bu anahtara göre gruplanır.
/// </param>
/// <param name="Question">API'ye olduğu gibi gönderilen Türkçe soru metni.</param>
/// <param name="ExpectedAnswer">
/// Raporda gerçek yanıtın yanında gösterilen, insanın okuyacağı referans yanıt. Puanlamaya katılmaz; puanlama yalnızca
/// <c>Expect</c> içindeki deterministik kontrollerle yapılır.
/// </param>
/// <param name="Expect">Yanıtın sağlaması gereken, makinece denetlenebilir beklentiler.</param>
public sealed record EvalQuestion(string Id, string Category, string Question, string ExpectedAnswer, EvalExpectation Expect);

/// <summary>
/// Bir sorunun yanıtının sağlaması gereken deterministik beklentiler; <see cref="EvalChecks"/> her birini ayrı bir
/// kontrole çevirir. Yanıtlanabilirlik her zaman denetlenir; <c>null</c> ya da boş bırakılan diğer beklentiler o soru
/// için denetlenmez.
/// </summary>
/// <remarks>
/// Kontroller bilinçli olarak kural tabanlıdır, LLM-as-judge değildir: aynı küçük modelle yargılamak zayıf ve tekrarlanamaz
/// olurdu. İçerik kontrolleri cümlenin tamamını değil olması gereken anahtar ifadeleri arar; böylece modelin farklı ama
/// doğru ifadeleri cezalandırılmaz.
/// </remarks>
/// <param name="Answerable">Sistemin yanıt vermesi mi (true) yoksa "bilgi yok" diyerek açıkça reddetmesi mi (false) gerektiği.</param>
/// <param name="SourcesAnyOf">Bu dokümanlardan en az birine atıf yapılmalıdır (aynı bilgi birden çok dokümanda geçebilir).</param>
/// <param name="SourcesAllOf">Bu dokümanların her birine atıf yapılmalıdır (iki dokümana yayılan sorular).</param>
/// <param name="ForbiddenSources">Bu dokümanların hiçbirine atıf yapılamaz (eski sürümler, güncelliğini yitirmiş bir SSS).</param>
/// <param name="MustContain">
/// Her iç liste bir VEYA grubudur; her gruptan en az bir ifade yanıtta geçmelidir (gruplar kendi aralarında VE ile
/// bağlanır). Gruplar aynı bilginin farklı söylenişlerini kabul etmek içindir (ör. "5 iş günü" / "beş iş günü").
/// </param>
/// <param name="MustNotContain">
/// Yanıtta geçmemesi gereken ifadeler: eski kural ("14 gün") ya da ters karar ("ücretsiz değil"). Doğru sayıyı
/// içeren ama kararı tersine çeviren bir yanıt yalnızca <c>MustContain</c> ile yakalanamaz.
/// </param>
/// <param name="DiscardedVersions">Yanıtın <c>versionResolution.discarded</c> listesinde elendiği raporlanması gereken eski sürümler.</param>
/// <param name="SectionsAnyOf">
/// Atıf yapılan bölümlerden en az biri bu bölüm adlarından birini içermelidir (ör. "Kargo Ücreti"). Doğru dokümana
/// ama yanlış bölüme dayanan bir yanıt doküman kontrolünden geçerdi.
/// </param>
/// <param name="ExpectConflict">
/// Yanıtta raporlanması gereken kaynaklar arası çelişki: seçilmesi gereken doküman, elenmesi gerekenler ve sunucunun
/// kurala uygunluk onayı (<c>ruleSatisfied=true</c>).
/// </param>
public sealed record EvalExpectation(
    bool Answerable,
    IReadOnlyList<string>? SourcesAnyOf = null,
    IReadOnlyList<string>? SourcesAllOf = null,
    IReadOnlyList<string>? ForbiddenSources = null,
    IReadOnlyList<IReadOnlyList<string>>? MustContain = null,
    IReadOnlyList<string>? MustNotContain = null,
    IReadOnlyList<string>? DiscardedVersions = null,
    IReadOnlyList<string>? SectionsAnyOf = null,
    ExpectedConflict? ExpectConflict = null);

/// <summary>
/// Bir yanıtta görünmesi beklenen kaynaklar arası çelişki. Kaynaklar arası çelişkiyi model bildirir; bu beklenti, modelin
/// çelişkiyi gerçekten fark ettiğini ve sunucunun öncelik kuralına göre doğru kaynağı seçtiğini birlikte denetler.
/// </summary>
/// <param name="Chosen">Geçerli kabul edilmesi gereken doküman (ör. güncel iade politikası).</param>
/// <param name="Rejected">Elenmesi gereken dokümanlar (ör. eski bilgi taşıyan SSS).</param>
public sealed record ExpectedConflict(string Chosen, IReadOnlyList<string> Rejected);

/// <summary>
/// API'nin <c>ApiResult</c> zarfı, istemcilerin aldığı biçimiyle (<c>success</c>, <c>message</c>, <c>data</c>,
/// <c>statusCode</c>).
/// </summary>
/// <remarks>
/// Araç API'yi dışarıdan, gerçek bir istemci gibi kullandığı için zarfın kendi kopyasını tanımlar. Sunucudaki
/// <c>ApiResult&lt;T&gt;</c> sınıfının setter'ları <c>private init</c> olduğundan System.Text.Json onu dolduramaz
/// (alanlar sessizce varsayılan değerde kalırdı); kurucu parametreli bu record ise sorunsuz deserialize edilir.
/// </remarks>
public sealed record ApiEnvelope<T>(bool Success, string Message, T? Data, int StatusCode);

/// <summary>
/// Tek bir deterministik kontrolün sonucu. <c>Name</c> raporda görünen Türkçe kontrol adıdır (ör. "kaynak",
/// "yasak ifade"); <c>Detail</c> beklenen ile gerçeği özetler ve rapora yalnızca kontrol kaldığında yazılır, böylece
/// kalmanın nedeni yanıt okunmadan görülür.
/// </summary>
public sealed record CheckResult(string Name, bool Passed, string Detail);

/// <summary>
/// Bir sorunun koşu sonucu: HTTP durumu, API mesajı, yanıtın kendisi, kontroller, uçtan uca süre ve iki arama modundaki
/// erişim isabeti. Hem <c>report.md</c> hem <c>results.json</c> bu kayıttan üretilir.
/// </summary>
/// <param name="Question">Değerlendirilen soru ve beklentileri.</param>
/// <param name="StatusCode">API'nin döndürdüğü HTTP durum kodu.</param>
/// <param name="Message">API zarfındaki mesaj; yanıt verisi yoksa raporda "Gerçek" olarak bu gösterilir.</param>
/// <param name="Answer">API'nin yanıtı; hata zarfında (veri yoksa) <c>null</c>.</param>
/// <param name="Checks">Yanıta uygulanan kontrollerin sonuçları.</param>
/// <param name="LatencyMs">İstemci tarafında ölçülen uçtan uca süre (ms); arama isabeti çağrıları dahil değildir.</param>
/// <param name="LexicalHit">Beklenen kaynak, yalnızca BM25 ile yapılan aramanın ilk sonuçları arasında mı (null: beklenen kaynağı olmayan soru).</param>
/// <param name="HybridHit">Beklenen kaynak, BM25 + vektör (hibrit) aramanın ilk sonuçları arasında mı (null: beklenen kaynağı olmayan soru).</param>
public sealed record QuestionResult(
    EvalQuestion Question,
    int StatusCode,
    string Message,
    AnswerDto? Answer,
    IReadOnlyList<CheckResult> Checks,
    long LatencyMs,
    bool? LexicalHit,
    bool? HybridHit)
{
    /// <summary>
    /// Soru, en az bir kontrol varsa ve hepsi geçtiyse geçmiş sayılır. <c>Count &gt; 0</c> koşulu bir emniyettir:
    /// <c>All</c> boş kümede <c>true</c> döndüğü için kontrolsüz bir sonuç aksi hâlde "geçti" sayılırdı.
    /// </summary>
    public bool Passed => Checks.Count > 0 && Checks.All(check => check.Passed);
}
