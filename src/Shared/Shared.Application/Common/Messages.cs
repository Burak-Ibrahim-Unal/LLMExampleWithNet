namespace Shared.Application.Common;

/// <summary>
/// API yanıtlarında kullanıcıya dönen tüm Türkçe mesajların tek kaynağı.
/// </summary>
/// <remarks>
/// Metinlerin tek yerde durması aynı durumun her uç noktada aynı sözcüklerle anlatılmasını sağlar, ifadeyi değiştirmeyi
/// tek satırlık bir iş hâline getirir ve testlerin metni kopyalamak yerine sabite başvurmasına izin verir. Sabitler
/// <c>const</c> olduğu için <c>[InlineData]</c> gibi attribute argümanlarında da kullanılabilir. Yer tutucu
/// (<c>{0}</c>) içeren mesajlar çağıran tarafta <c>string.Format</c> ile doldurulur. Sunucu loglarına yazılan İngilizce
/// mesajlar burada değildir; bu sınıf yalnızca API yanıtına giren metinleri içerir.
/// </remarks>
public static class Messages
{
    /// <summary>
    /// Knowledge modülünün (ingestion, arama, doküman ve soru uç noktaları) mesajları. Modül başına bir iç sınıf,
    /// başka bir modül eklendiğinde mesajların karışmadan ayrı tutulmasını sağlar.
    /// </summary>
    public static class Knowledge
    {
        /// <summary>
        /// Ingestion başarıyla tamamlandığında (açılışta veya <c>POST /v1/documents/reindex</c> ile) 200 yanıtının
        /// mesajı. Embedding servisi erişilemese de ingestion başarılı sayılır; o durumda ayrıntı özetteki
        /// <c>warning</c> alanındadır (<see cref="EmbeddingUnavailable"/>).
        /// </summary>
        public const string Reindexed = "Bilgi tabanı indekslendi.";
        /// <summary>
        /// Bilgi tabanı okunabildiği hâlde içinde hiç doküman yoksa ingestion'ın döndürdüğü 422 mesajı. Bu durumda
        /// mevcut kayıtlar ve indeks olduğu gibi kalır; boş bir indeksi "başarılı" diye kabul etmek sonraki her soruyu
        /// sessizce yanıtsız bırakırdı.
        /// </summary>
        public const string KnowledgeBaseEmpty = "Bilgi tabanı klasöründe doküman bulunamadı.";
        /// <summary>
        /// Bilgi tabanı dosyaları G/Ç veya erişim hatası (<c>IOException</c>, <c>UnauthorizedAccessException</c>)
        /// yüzünden okunamadığında ingestion'ın döndürdüğü 422 mesajı. Dosya yolu gibi sunucu ayrıntıları istemciye
        /// sızmasın diye mesaj genel tutulur; asıl exception sunucu loguna yazılır.
        /// </summary>
        public const string KnowledgeBaseUnreadable = "Bilgi tabanı dosyaları okunamadı; ayrıntılar sunucu loglarında.";
        /// <summary>
        /// İki veya daha fazla dosya front matter'da aynı <c>id</c> değerini kullandığında ingestion'ın döndürdüğü 422
        /// mesajı; <c>{0}</c> yerine çakışan kimlikler virgülle ayrılarak yazılır. Bu kimlik kayıtların dosyalarla
        /// eşleştirildiği anahtardır ve veritabanında benzersizdir; çakışma sessizce geçilseydi hangi dosyanın geçerli
        /// olduğu belirsizleşirdi.
        /// </summary>
        public const string DuplicateDocumentId = "Aynı doküman kimliği birden fazla dosyada kullanılmış: {0}";
        /// <summary>
        /// Embedding servisine ulaşılamadığında ingestion özetinin <c>warning</c> alanına yazılan uyarı (açılışta ayrıca
        /// loglanır). Hata değildir: ingestion 200 ile tamamlanır, arama BM25'e düşer ve sonraki bir reindex eksik
        /// vektörleri tamamlar.
        /// </summary>
        public const string EmbeddingUnavailable = "Embedding servisine ulaşılamadı; arama yalnızca anahtar kelime (BM25) moduyla çalışıyor.";
        /// <summary>
        /// Arama indeksi henüz kurulmamışken (ör. açılıştaki ingestion başarısız olduysa) soru ve arama isteklerine dönen
        /// 503 mesajı. Kullanıcıya ne yapabileceğini de söyler: biraz beklemek veya reindex'i tetiklemek.
        /// </summary>
        public const string IndexNotReady = "Bilgi tabanı henüz hazır değil. Biraz sonra tekrar deneyin veya POST /v1/documents/reindex çağırın.";
        /// <summary><c>GET /v1/documents/{id}</c> isteğinde verilen kimlikte doküman yoksa dönen 404 mesajı.</summary>
        public const string DocumentNotFound = "Doküman bulunamadı.";
        /// <summary><c>GET /v1/search</c> isteğinde <c>q</c> eksik, boş veya yalnızca boşluksa dönen 400 mesajı.</summary>
        public const string QueryRequired = "Arama ifadesi boş olamaz.";
        /// <summary>
        /// Arama ifadesi <c>KnowledgeBusinessRules.MaxQueryLength</c> (500) karakteri aştığında dönen 400 mesajı;
        /// <c>{0}</c> yerine sınır yazılır. Sınır, aşırı uzun girdilerin arama ve embedding maliyetini öngörülebilir tutar.
        /// </summary>
        public const string QueryTooLong = "Arama ifadesi en fazla {0} karakter olabilir.";
        /// <summary>
        /// Aramada <c>topK</c> 1 ile <c>KnowledgeBusinessRules.MaxTopK</c> (20) arasında değilse dönen 400 mesajı;
        /// <c>{0}</c> yerine üst sınır yazılır.
        /// </summary>
        public const string TopKOutOfRange = "topK 1 ile {0} arasında olmalıdır.";
        /// <summary>
        /// Aramada <c>mode</c> parametresi <c>lexical</c> veya <c>hybrid</c> dışında bir değer aldığında dönen 400 mesajı.
        /// Bilinmeyen bir değeri sessizce varsayılana çevirmek, değerlendirmede yanlış modun ölçülmesine yol açabilirdi.
        /// </summary>
        public const string RetrievalModeInvalid = "mode yalnızca 'lexical' veya 'hybrid' olabilir.";
        /// <summary><c>POST /v1/questions</c> isteğinde soru boş veya yalnızca boşluksa dönen 400 mesajı.</summary>
        public const string QuestionRequired = "Soru boş olamaz.";
        /// <summary>
        /// Soru <c>KnowledgeBusinessRules.MaxQueryLength</c> (500) karakteri aştığında dönen 400 mesajı; <c>{0}</c> yerine
        /// sınır yazılır. Sınır, embedding ve dil modeli çağrılarının maliyetini ve prompt boyutunu öngörülebilir tutar.
        /// </summary>
        public const string QuestionTooLong = "Soru en fazla {0} karakter olabilir.";
        /// <summary>
        /// Yanıt geri çevrildiğinde (<c>answerable=false</c>) hem yanıt metni hem de 200 zarfının mesajı olarak kullanılan
        /// sabit metin. Ödevin "dokümanlarda yeterli bilgi yoksa bunu açıkça söyle" şartını karşılar: sistem bir yanıt
        /// üretmek yerine her zaman aynı, öngörülebilir cümleyi döner. "Bilmiyorum" geçerli bir iş sonucu olduğu için HTTP
        /// durumu 200'dür; hangi kapıda geri çevrildiği <c>refusalReason</c> alanında taşınır.
        /// </summary>
        public const string NotEnoughInformation = "Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı.";
        /// <summary>
        /// Dil modeli sunucusuna ulaşılamadığında veya model hiç yapılandırılmadığında (<c>Llm:BaseUrl</c> boş) soru
        /// isteğine dönen 503 mesajı. "Yeterli bilgi yok" yanıtından farklıdır: sorun dokümanlarda değil geçici olarak
        /// serviste olduğu için istemciye yeniden denemesi söylenir. Kapı 1'de geri çevrilen sorular modele hiç
        /// gitmediğinden bu durumdan etkilenmez.
        /// </summary>
        public const string LlmUnavailable = "Dil modeli servisine şu anda ulaşılamıyor. Lütfen daha sonra tekrar deneyin.";
        /// <summary>
        /// Model, soru başına model çağrısı bütçesi içinde (düzeltici talimatla yapılan yeniden deneme dahil) şemaya uygun
        /// JSON (ya da <c>answerable=true</c> iken boş olmayan bir yanıt) üretemediğinde soru isteğine dönen 502 mesajı. 502,
        /// hatanın bu API'de değil yukarı akıştaki model sunucusunda olduğunu belirtir.
        /// </summary>
        public const string LlmInvalidOutput = "Dil modeli geçerli bir yanıt üretemedi. Lütfen tekrar deneyin.";
        /// <summary>
        /// Soru, dil modelinin talimatlarını değiştirmeye yönelik ifadeler içerdiği için (prompt injection) model
        /// çağrılmadan reddedildiğinde yanıt metni ve 200 zarfının mesajı. "Bilgi yok" mesajından ayrıdır, çünkü sorun
        /// dokümanlarda değil sorunun kendisindedir. Hangi kalıbın yakalandığı bilerek söylenmez.
        /// </summary>
        public const string PromptInjectionRefused =
            "Soru, asistanın çalışma talimatlarını değiştirmeye yönelik ifadeler içerdiği için yanıtlanmadı. Lütfen yalnızca destek sorunuzu yazın.";
        /// <summary>
        /// Modelin çıktısı güvenlik denetiminden geçmediğinde (sistem prompt'u sızıntısı) yanıt metni ve 200 zarfının
        /// mesajı. Hangi denetimin tetiklendiği bilerek söylenmez; prompt injection reddindeki gibi saldırgana kalıpların
        /// etrafından dolaşması için ipucu verilmez.
        /// </summary>
        public const string UnsafeOutputRefused =
            "Üretilen yanıt güvenlik denetiminden geçmediği için gösterilmedi. Lütfen sorunuzu farklı bir şekilde yeniden sorun.";
    }

    /// <summary>
    /// API genelindeki koruma katmanlarının (hız sınırı, yönetici anahtarı, istek boyutu) mesajları. Tek bir modüle ait
    /// olmadıkları için ayrı bir iç sınıfta durur; zarf ve dil diğer yanıtlarla aynıdır.
    /// </summary>
    public static class Security
    {
        /// <summary>
        /// İstemci, soru ucunun dakika başına istek sınırını aştığında 429 yanıtının mesajı. Ne kadar bekleneceği
        /// <c>Retry-After</c> başlığında saniye olarak verilir.
        /// </summary>
        public const string TooManyRequests = "Çok fazla istek gönderildi. Lütfen biraz bekleyip tekrar deneyin.";

        /// <summary>
        /// Yönetici işlemine (<c>POST /v1/documents/reindex</c>) <c>X-Admin-Key</c> başlığı olmadan ya da yanlış bir
        /// anahtarla gelindiğinde 401 yanıtının mesajı. Eksik ve yanlış anahtar aynı mesajı alır; anahtarın varlığı ya da
        /// biçimi hakkında ipucu verilmez.
        /// </summary>
        public const string AdminKeyRequired = "Bu işlem için geçerli bir yönetici anahtarı (X-Admin-Key başlığı) gerekli.";

        /// <summary>
        /// Sunucuda yönetici anahtarı tanımlı değilken yönetici işlemine gelindiğinde 403 yanıtının mesajı. Uç güvenli
        /// varsayılanla kapalıdır; operatör anahtarı tanımlayarak açar. Açılıştaki indeksleme bundan etkilenmez.
        /// </summary>
        public const string ReindexDisabled =
            "Yeniden indeksleme ucu kapalı: sunucuda yönetici anahtarı (Security__AdminApiKey) tanımlı değil.";

        /// <summary>
        /// İstek gövdesi izin verilen boyutu aştığında 413 yanıtının mesajı; <c>{0}</c> bayt cinsinden sınırdır. Bir destek
        /// sorusu en fazla 500 karakterdir, sınır bunun çok üstündedir; aşan istek okunmadan reddedilir.
        /// </summary>
        public const string RequestTooLarge = "İstek gövdesi çok büyük; en fazla {0} bayt gönderilebilir.";
    }
}
