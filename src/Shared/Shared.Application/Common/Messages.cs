using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Shared.Application.Common;

/// <summary>
/// Kullanıcıya dönen bütün Türkçe metinlerin erişim noktası: API mesajları, yanıtta görünen kural ve gerekçe metinleri,
/// bilgi tabanı biçim hataları ve OpenAPI açıklamaları. Metinlerin kendisi kodda değil
/// <c>Common/Resources/messages.json</c> dosyasındadır; bu sınıf yalnızca her metnin ne zaman kullanıldığını belgeler ve
/// dosyadaki karşılığını döndürür.
/// </summary>
/// <remarks>
/// <para>
/// Metinlerin tek dosyada durması aynı durumun her uç noktada aynı sözcüklerle anlatılmasını sağlar, ifadeyi kod
/// değişikliği olmadan düzenlemeye ve başka bir dile çevirmeye izin verir; testler metni kopyalamak yerine özelliğe
/// başvurur. Dosyadaki anahtar, iç sınıfın ve özelliğin adıdır (<c>Knowledge.NotEnoughInformation</c>); anahtarlar
/// kodda yazılmaz, <c>nameof</c> ve <see cref="CallerMemberNameAttribute"/> ile türetilir. Dosyada olmayan bir anahtar
/// ilk kullanımda açık bir hatayla durur; bir birim testi her özelliğin dosyada, dosyadaki her metnin bir özellikte
/// karşılığı olduğunu doğrular.
/// </para>
/// <para>
/// Yer tutucu (<c>{0}</c>) içeren mesajlar çağıran tarafta <c>string.Format</c> ile doldurulur. Sunucu loglarına yazılan
/// İngilizce mesajlar ve programcı hatalarına ait istisna metinleri burada değildir; onlar kullanıcıya dönmez.
/// </para>
/// </remarks>
public static class Messages
{
    /// <summary>Gömülü mesaj dosyasının adı; proje dosyasında <c>LogicalName</c> ile sabitlenir.</summary>
    private const string ResourceName = "Shared.Application.Common.Resources.messages.json";

    /// <summary>Dosyadaki bütün metinler, <c>Bölüm.Ad</c> anahtarıyla; ilk kullanımda bir kez okunur.</summary>
    private static readonly IReadOnlyDictionary<string, string> Texts = Load();

    /// <summary>Dosyadaki bütün anahtarlar; birim testleri her metnin bir özellikte karşılığı olduğunu bununla doğrular.</summary>
    internal static IReadOnlyCollection<string> Keys => [.. Texts.Keys];

    /// <summary>
    /// Bir bölümdeki metni döndürür; ad, çağıran özelliğin adıdır. Dosyada yoksa <see cref="InvalidOperationException"/>
    /// fırlatır.
    /// </summary>
    /// <param name="section">İç sınıfın adı (ör. <c>Knowledge</c>).</param>
    /// <param name="name">Metnin adı; derleyici çağıran özelliğin adını verir.</param>
    private static string Text(string section, [CallerMemberName] string name = "") =>
        Texts.TryGetValue($"{section}.{name}", out var text)
            ? text
            : throw new InvalidOperationException($"The message '{section}.{name}' is missing from {ResourceName}.");

    /// <summary>
    /// Gömülü dosyayı okur ve iki düzeyli yapıyı (bölüm → ad → metin) <c>Bölüm.Ad</c> anahtarlı tek bir sözlüğe çevirir.
    /// Dosya bulunamaz ya da bir değer metin değilse açılış açık bir hatayla durur.
    /// </summary>
    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(Messages).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The message resource '{ResourceName}' is missing from the assembly.");
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.EnumerateObject()
            .SelectMany(section => section.Value.EnumerateObject().Select(entry => (Key: $"{section.Name}.{entry.Name}", Text: entry.Value.GetString())))
            .ToDictionary(entry => entry.Key, entry => entry.Text ?? throw new InvalidOperationException($"The message '{entry.Key}' is null."), StringComparer.Ordinal);
    }

    /// <summary>
    /// Knowledge modülünün (ingestion, arama, doküman ve soru uç noktaları) mesajları. Modül başına bir iç sınıf,
    /// başka bir modül eklendiğinde mesajların karışmadan ayrı tutulmasını sağlar.
    /// </summary>
    public static class Knowledge
    {
        /// <summary>Dosyadaki bölümün adı.</summary>
        private const string Section = nameof(Knowledge);

        /// <summary>
        /// Ingestion başarıyla tamamlandığında (açılışta veya <c>POST /v1/documents/reindex</c> ile) 200 yanıtının
        /// mesajı. Embedding servisi erişilemese de ingestion başarılı sayılır; o durumda ayrıntı özetteki
        /// <c>warning</c> alanındadır (<see cref="EmbeddingUnavailable"/>).
        /// </summary>
        public static string Reindexed => Text(Section);

        /// <summary>
        /// Bilgi tabanı okunabildiği hâlde içinde hiç doküman yoksa ingestion'ın döndürdüğü 422 mesajı. Bu durumda
        /// mevcut kayıtlar ve indeks olduğu gibi kalır; boş bir indeksi "başarılı" diye kabul etmek sonraki her soruyu
        /// sessizce yanıtsız bırakırdı.
        /// </summary>
        public static string KnowledgeBaseEmpty => Text(Section);

        /// <summary>
        /// Bilgi tabanı dosyaları G/Ç veya erişim hatası (<c>IOException</c>, <c>UnauthorizedAccessException</c>)
        /// yüzünden okunamadığında ingestion'ın döndürdüğü 422 mesajı. Dosya yolu gibi sunucu ayrıntıları istemciye
        /// sızmasın diye mesaj genel tutulur; asıl exception sunucu loguna yazılır.
        /// </summary>
        public static string KnowledgeBaseUnreadable => Text(Section);

        /// <summary>
        /// İki veya daha fazla dosya front matter'da aynı <c>id</c> değerini kullandığında ingestion'ın döndürdüğü 422
        /// mesajı; <c>{0}</c> yerine çakışan kimlikler virgülle ayrılarak yazılır. Bu kimlik kayıtların dosyalarla
        /// eşleştirildiği anahtardır ve veritabanında benzersizdir; çakışma sessizce geçilseydi hangi dosyanın geçerli
        /// olduğu belirsizleşirdi.
        /// </summary>
        public static string DuplicateDocumentId => Text(Section);

        /// <summary>
        /// Embedding servisine ulaşılamadığında ingestion özetinin <c>warning</c> alanına yazılan uyarı (açılışta ayrıca
        /// loglanır). Hata değildir: ingestion 200 ile tamamlanır, arama BM25'e düşer ve sonraki bir reindex eksik
        /// vektörleri tamamlar.
        /// </summary>
        public static string EmbeddingUnavailable => Text(Section);

        /// <summary>
        /// Arama indeksi henüz kurulmamışken (ör. açılıştaki ingestion başarısız olduysa) soru ve arama isteklerine dönen
        /// 503 mesajı. Kullanıcıya ne yapabileceğini de söyler: biraz beklemek veya reindex'i tetiklemek.
        /// </summary>
        public static string IndexNotReady => Text(Section);

        /// <summary><c>GET /v1/documents/{id}</c> isteğinde verilen kimlikte doküman yoksa dönen 404 mesajı.</summary>
        public static string DocumentNotFound => Text(Section);

        /// <summary><c>GET /v1/search</c> isteğinde <c>q</c> eksik, boş veya yalnızca boşluksa dönen 400 mesajı.</summary>
        public static string QueryRequired => Text(Section);

        /// <summary>
        /// Arama ifadesi <c>KnowledgeBusinessRules.MaxQueryLength</c> (500) karakteri aştığında dönen 400 mesajı;
        /// <c>{0}</c> yerine sınır yazılır. Sınır, aşırı uzun girdilerin arama ve embedding maliyetini öngörülebilir tutar.
        /// </summary>
        public static string QueryTooLong => Text(Section);

        /// <summary>
        /// Aramada <c>topK</c> 1 ile <c>KnowledgeBusinessRules.MaxTopK</c> (20) arasında değilse dönen 400 mesajı;
        /// <c>{0}</c> yerine üst sınır yazılır.
        /// </summary>
        public static string TopKOutOfRange => Text(Section);

        /// <summary>
        /// Aramada <c>mode</c> parametresi <c>lexical</c> veya <c>hybrid</c> dışında bir değer aldığında dönen 400 mesajı.
        /// Bilinmeyen bir değeri sessizce varsayılana çevirmek, değerlendirmede yanlış modun ölçülmesine yol açabilirdi.
        /// </summary>
        public static string RetrievalModeInvalid => Text(Section);

        /// <summary><c>POST /v1/questions</c> isteğinde soru boş veya yalnızca boşluksa dönen 400 mesajı.</summary>
        public static string QuestionRequired => Text(Section);

        /// <summary>
        /// Soru <c>KnowledgeBusinessRules.MaxQueryLength</c> (500) karakteri aştığında dönen 400 mesajı; <c>{0}</c> yerine
        /// sınır yazılır. Sınır, embedding ve dil modeli çağrılarının maliyetini ve prompt boyutunu öngörülebilir tutar.
        /// </summary>
        public static string QuestionTooLong => Text(Section);

        /// <summary>
        /// Yanıt geri çevrildiğinde (<c>answerable=false</c>) hem yanıt metni hem de 200 zarfının mesajı olarak kullanılan
        /// sabit metin. Ödevin "dokümanlarda yeterli bilgi yoksa bunu açıkça söyle" şartını karşılar: sistem bir yanıt
        /// üretmek yerine her zaman aynı, öngörülebilir cümleyi döner. "Bilmiyorum" geçerli bir iş sonucu olduğu için HTTP
        /// durumu 200'dür; hangi kapıda geri çevrildiği <c>refusalReason</c> alanında taşınır.
        /// </summary>
        public static string NotEnoughInformation => Text(Section);

        /// <summary>
        /// Dil modeli sunucusuna ulaşılamadığında veya model hiç yapılandırılmadığında (<c>Llm:BaseUrl</c> boş) soru
        /// isteğine dönen 503 mesajı. "Yeterli bilgi yok" yanıtından farklıdır: sorun dokümanlarda değil geçici olarak
        /// serviste olduğu için istemciye yeniden denemesi söylenir. Kapı 1'de geri çevrilen sorular modele hiç
        /// gitmediğinden bu durumdan etkilenmez.
        /// </summary>
        public static string LlmUnavailable => Text(Section);

        /// <summary>
        /// Model, soru başına model çağrısı bütçesi içinde (düzeltici talimatla yapılan yeniden deneme dahil) şemaya uygun
        /// JSON (ya da <c>answerable=true</c> iken boş olmayan bir yanıt) üretemediğinde soru isteğine dönen 502 mesajı. 502,
        /// hatanın bu API'de değil yukarı akıştaki model sunucusunda olduğunu belirtir.
        /// </summary>
        public static string LlmInvalidOutput => Text(Section);

        /// <summary>
        /// Soru, dil modelinin talimatlarını değiştirmeye yönelik ifadeler içerdiği için (prompt injection) model
        /// çağrılmadan reddedildiğinde yanıt metni ve 200 zarfının mesajı. "Bilgi yok" mesajından ayrıdır, çünkü sorun
        /// dokümanlarda değil sorunun kendisindedir. Hangi kalıbın yakalandığı bilerek söylenmez.
        /// </summary>
        public static string PromptInjectionRefused => Text(Section);

        /// <summary>
        /// Modelin çıktısı güvenlik denetiminden geçmediğinde (sistem prompt'u sızıntısı) yanıt metni ve 200 zarfının
        /// mesajı. Hangi denetimin tetiklendiği bilerek söylenmez; prompt injection reddindeki gibi saldırgana kalıpların
        /// etrafından dolaşması için ipucu verilmez.
        /// </summary>
        public static string UnsafeOutputRefused => Text(Section);
    }

    /// <summary>
    /// Yanıtın içinde görünen ve kararların nasıl verildiğini açıklayan metinler: sürüm seçim kuralı, kaynak önceliği
    /// kuralı, elenen sürümlerin gerekçeleri ve sunucunun öncelik kuralını uyguladığı çelişki kayıtlarının gerekçesi.
    /// </summary>
    public static class Answering
    {
        /// <summary>Dosyadaki bölümün adı.</summary>
        private const string Section = nameof(Answering);

        /// <summary>
        /// Sürüm seçim kuralı. Her yanıtta (retlerde de) <c>versionResolution.rule</c> alanında döner; istemci güncel
        /// sürümün hangi kurala göre seçildiğini yanıttan okuyabilir.
        /// </summary>
        public static string VersionRule => Text(Section);

        /// <summary>
        /// Farklı dokümanlar çeliştiğinde geçerli kaynağı belirleyen öncelik kuralı (politika ve prosedür &gt; kılavuz &gt;
        /// SSS, aynı türde daha yeni yürürlük tarihi). Sunucu kuralı zorladığında çelişki kaydının gerekçesine yazılır.
        /// </summary>
        public static string PrecedenceRule => Text(Section);

        /// <summary>
        /// Henüz yürürlüğe girmemiş bir sürümün elenme gerekçesi; <c>{0}</c> yerine yürürlük tarihi (<c>yyyy-MM-dd</c>)
        /// yazılır.
        /// </summary>
        public static string NotYetInEffect => Text(Section);

        /// <summary>Ailesinde yürürlükte hiçbir sürüm kalmamış bir dokümanın elenme gerekçesi.</summary>
        public static string NoVersionInEffect => Text(Section);

        /// <summary>
        /// Güncel bir sürüm tarafından geçersiz kılınmış sürümün elenme gerekçesi; <c>{0}</c> güncel sürüm numarası,
        /// <c>{1}</c> onun yürürlük tarihidir (<c>yyyy-MM-dd</c>).
        /// </summary>
        public static string SupersededBy => Text(Section);

        /// <summary>
        /// Model bir çelişkide daha düşük öncelikli kaynağı seçtiği için sunucunun kararı düzelttiği çelişki kaydının
        /// gerekçesi; <c>{0}</c> yerine <see cref="PrecedenceRule"/> yazılır. Kararın modelden değil sunucudan geldiğini ve
        /// hangi kurala dayandığını yanıtın içinde açıkça söyler.
        /// </summary>
        public static string PrecedenceEnforced => Text(Section);
    }

    /// <summary>
    /// Bilgi tabanı okunurken bulunan biçim hataları. Ingestion bunları 422 yanıtının mesajı olarak döndürür;
    /// dosyaya ait olanların önüne dosya adı eklenir (<c>dosya: neden</c>).
    /// </summary>
    public static class Ingestion
    {
        /// <summary>Dosyadaki bölümün adı.</summary>
        private const string Section = nameof(Ingestion);

        /// <summary>Yapılandırılan bilgi tabanı klasörü bulunamadığında dönen mesaj; <c>{0}</c> aranan yoldur.</summary>
        public static string FolderNotFound => Text(Section);

        /// <summary>Dosya YAML front matter (<c>---</c>) ile başlamıyorsa dönen neden.</summary>
        public static string FrontMatterMissing => Text(Section);

        /// <summary>Front matter'ın kapanış satırı (<c>---</c>) bulunamadığında dönen neden.</summary>
        public static string FrontMatterNotClosed => Text(Section);

        /// <summary>Front matter'dan sonra hiç içerik bölümü yoksa dönen neden.</summary>
        public static string ContentMissing => Text(Section);

        /// <summary>Front matter geçerli YAML değilse dönen neden; <c>{0}</c> ayrıştırıcının iletisidir.</summary>
        public static string FrontMatterUnreadable => Text(Section);

        /// <summary>Zorunlu bir front matter alanı eksik ya da boşsa dönen neden; <c>{0}</c> alanın adıdır.</summary>
        public static string FieldMissing => Text(Section);

        /// <summary><c>effectiveDate</c> <c>yyyy-MM-dd</c> biçiminde değilse dönen neden; <c>{0}</c> okunan değerdir.</summary>
        public static string DateInvalid => Text(Section);

        /// <summary><c>status</c> bilinen değerlerden biri değilse dönen neden; <c>{0}</c> okunan değerdir.</summary>
        public static string StatusUnknown => Text(Section);

        /// <summary><c>category</c> bilinen değerlerden biri değilse dönen neden; <c>{0}</c> okunan değerdir.</summary>
        public static string CategoryUnknown => Text(Section);
    }

    /// <summary>
    /// API genelindeki koruma katmanlarının (hız sınırı, yönetici anahtarı, istek boyutu) mesajları. Tek bir modüle ait
    /// olmadıkları için ayrı bir iç sınıfta durur; zarf ve dil diğer yanıtlarla aynıdır.
    /// </summary>
    public static class Security
    {
        /// <summary>Dosyadaki bölümün adı.</summary>
        private const string Section = nameof(Security);

        /// <summary>
        /// İstemci, soru ucunun dakika başına istek sınırını aştığında 429 yanıtının mesajı. En fazla ne kadar
        /// bekleneceği <c>Retry-After</c> başlığında saniye olarak verilir.
        /// </summary>
        public static string TooManyRequests => Text(Section);

        /// <summary>
        /// Yönetici işlemine (<c>POST /v1/documents/reindex</c>) <c>X-Admin-Key</c> başlığı olmadan ya da yanlış bir
        /// anahtarla gelindiğinde 401 yanıtının mesajı. Eksik ve yanlış anahtar aynı mesajı alır; anahtarın varlığı ya da
        /// biçimi hakkında ipucu verilmez.
        /// </summary>
        public static string AdminKeyRequired => Text(Section);

        /// <summary>
        /// Sunucuda yönetici anahtarı tanımlı değilken yönetici işlemine gelindiğinde 403 yanıtının mesajı. Uç güvenli
        /// varsayılanla kapalıdır; operatör anahtarı tanımlayarak açar. Açılıştaki indeksleme bundan etkilenmez.
        /// </summary>
        public static string ReindexDisabled => Text(Section);

        /// <summary>
        /// İstek gövdesi izin verilen boyutu aştığında 413 yanıtının mesajı; <c>{0}</c> bayt cinsinden sınırdır. Bir destek
        /// sorusu en fazla 500 karakterdir, sınır bunun çok üstündedir; aşan istek okunmadan reddedilir.
        /// </summary>
        public static string RequestTooLarge => Text(Section);
    }

    /// <summary>
    /// İsteğin kendisi okunamadığında (geçersiz JSON gövdesi, beklenen türe çevrilemeyen bir alan) dönen 400 mesajları.
    /// Bu hatalar uç noktaya ulaşmadan, istek bağlanırken oluşur; çerçevenin İngilizce varsayılan hata biçimi yerine
    /// diğer bütün hatalar gibi <c>ApiResult</c> zarfıyla ve Türkçe mesajla döner.
    /// </summary>
    public static class Request
    {
        /// <summary>Dosyadaki bölümün adı.</summary>
        private const string Section = nameof(Request);

        /// <summary>Hangi alanın sorunlu olduğu bilinmediğinde (ör. gövde hiç JSON değil) dönen mesaj.</summary>
        public static string Unreadable => Text(Section);

        /// <summary>
        /// Sorunlu alanlar bilindiğinde dönen mesaj; <c>{0}</c> virgülle ayrılmış alan adlarıdır (ör. <c>topK</c>,
        /// <c>question</c>). Alan adı, istemcinin neyi düzelteceğini çerçevenin ayrıntılı İngilizce iletisine gerek
        /// kalmadan söyler.
        /// </summary>
        public static string UnreadableFields => Text(Section);
    }

    /// <summary>
    /// OpenAPI belgesindeki (<c>/openapi/v1.json</c>, Scalar arayüzü) uç nokta özetleri ve açıklamaları. API'yi kullanan
    /// geliştiricinin gördüğü metinlerdir; diğer mesajlar gibi Türkçedir ve dosyada durur.
    /// </summary>
    public static class ApiDocs
    {
        /// <summary>Dosyadaki bölümün adı.</summary>
        private const string Section = nameof(ApiDocs);

        /// <summary><c>POST /v1/questions</c> özeti.</summary>
        public static string AskQuestionSummary => Text(Section);

        /// <summary><c>POST /v1/questions</c> açıklaması: yanıtın kaynak, sürüm kararı ve çelişki alanları.</summary>
        public static string AskQuestionDescription => Text(Section);

        /// <summary><c>GET /v1/search</c> özeti.</summary>
        public static string SearchSummary => Text(Section);

        /// <summary><c>GET /v1/documents</c> özeti.</summary>
        public static string ListDocumentsSummary => Text(Section);

        /// <summary><c>GET /v1/documents/{id}</c> özeti.</summary>
        public static string GetDocumentSummary => Text(Section);

        /// <summary><c>POST /v1/documents/reindex</c> özeti.</summary>
        public static string ReindexSummary => Text(Section);

        /// <summary><c>POST /v1/documents/reindex</c> açıklaması: yönetici anahtarı ve 401/403 durumları.</summary>
        public static string ReindexDescription => Text(Section);

        /// <summary><c>GET /v1/health</c> özeti.</summary>
        public static string HealthSummary => Text(Section);
    }
}
