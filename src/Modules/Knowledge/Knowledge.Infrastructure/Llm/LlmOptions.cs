namespace Knowledge.Infrastructure.Llm;

/// <summary>
/// Dil modeli ucunun ayarları; yapılandırmadaki <c>Llm</c> bölümüne bağlanır (ortam değişkenlerinde
/// <c>Llm__BaseUrl</c> biçiminde, Development ortamında <c>.env</c> dosyasından). Sağlayıcı (llama.cpp, LM Studio,
/// Ollama, OpenAI, Gemini) ve örnekleme davranışı kod değişmeden yalnızca bu ayarlarla değiştirilir.
/// </summary>
public sealed class LlmOptions
{
    /// <summary>
    /// Bağlanılan yapılandırma bölümünün adı. Ortam değişkenlerinde çift alt çizgi bölüm ayırıcıdır
    /// (<c>Llm__ChatModel</c> → <c>Llm:ChatModel</c>).
    /// </summary>
    public const string SectionName = "Llm";

    /// <summary>
    /// OpenAI-uyumlu sohbet ucunun kök adresi (llama.cpp sunucusu, LM Studio, Ollama, OpenAI, Gemini). Değer
    /// <c>.env</c> dosyasından veya ortam değişkeninden gelir, koda yazılmaz. Boşsa
    /// <see cref="UnconfiguredAnswerGenerator"/> kullanılır: uygulama yine açılır, arama ve doküman uçları çalışır,
    /// modele ulaşması gereken sorular net bir 503 alır.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Sağlayıcının API anahtarı. Varsayılan <c>"local"</c> bir yer tutucudur: yerel sunucular anahtarı yok sayar ama
    /// OpenAI istemcisi boş olmayan bir değer ister. Bulut sağlayıcının gerçek anahtarı yalnızca ortam değişkeni veya
    /// <c>.env</c> üzerinden verilir; asla kaynak koda yazılmaz.
    /// </summary>
    public string ApiKey { get; set; } = "local";

    /// <summary>
    /// Model adı. Tek model sunan sunucular (llama.cpp gibi) bu adı yok sayar; yine de loglanır, sağlık uç noktasında
    /// ve yanıtın <c>diagnostics</c> bölümünde raporlanır. Sunucunun döndürdüğü model kimliği yerine bu ad raporlanır,
    /// çünkü llama.cpp orada yerel model dosyasının yolunu döndürür ve bu makine ayrıntısı dışarı sızmamalıdır.
    /// </summary>
    public string ChatModel { get; set; } = "gemma-4-26b-a4b-it";

    /// <summary>
    /// Destekleyen chat şablonları için düşünme (thinking) modu anahtarı; istekte
    /// <c>chat_template_kwargs.enable_thinking</c> olarak gönderilir (bkz. <see cref="ChatTemplateKwargsPolicy"/>).
    /// <c>null</c> alanı hiç göndermez ve sunucunun varsayılanını bırakır.
    /// </summary>
    /// <remarks>
    /// Gemma 4'te düşünme modu sunucu tarafında varsayılan olarak açıktır. Değerlendirmede kapalı mod aynı doğruluğu
    /// yaklaşık 7 kat daha hızlı verdiği için örnek <c>.env</c> bu değeri <c>false</c> yapar. Bu alanı tanımayan
    /// sağlayıcılarda (OpenAI, Gemini) değer boş bırakılmalıdır; böylece alan isteğe hiç eklenmez ve bilinmeyen bir
    /// alan yüzünden isteğin reddedilmesi riski oluşmaz.
    /// </remarks>
    public bool? EnableThinking { get; set; }

    /// <summary>
    /// Örnekleme sıcaklığı. 0 ile aynı soru ve kaynaklar için tekrarlanabilir yanıtlar alınır ve değerlendirme sonuçları
    /// koşudan koşuya değişmez; kaynağa dayalı bir destek yanıtında yaratıcılığa ihtiyaç yoktur. <c>null</c> değeri
    /// istekte gönderilmez ve sağlayıcının varsayılanı geçerli olur.
    /// </summary>
    public float? Temperature { get; set; } = 0f;

    /// <summary>
    /// Örnekleme tohumu (seed). Sıcaklık 0 ile birlikte, destekleyen sunucularda (llama.cpp gibi) çıktının
    /// tekrarlanabilirliğini artırır. <c>null</c> değeri istekte gönderilmez.
    /// </summary>
    public long? Seed { get; set; } = 42;

    /// <summary>
    /// Yanıt için üretilebilecek en fazla token sayısı. Bilinçli olarak cömerttir: düşünme modunda akıl yürütme
    /// token'ları aynı bütçeden harcanır ve küçük bir sınır JSON yanıtına yer bırakmaz; <c>content</c> boş döner ve
    /// çıktı geçersiz sayılır. Değerlendirmede yanıt başına düşünme kapalıyken 60–280, açıkken 654–2618 token üretildi.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 4096;

    /// <summary>
    /// Model isteği başına ağ zaman aşımı (saniye). Uzak ve tek slotlu bir sunucuda ilk istek (ısınma) veya o anki yük
    /// tek bir yanıtı onlarca saniyeye çıkarabildiği için cömerttir; yine de sınırlıdır ve süre dolduğunda soru,
    /// süresiz beklemek yerine 503 alır.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// <c>true</c>: JSON şeması <c>response_format</c> ile gönderilir; llama.cpp şemayı bir grammar'a çevirir ve çıktı
    /// her zaman şemaya uygun ayrıştırılabilir. <c>false</c>: şema bunun yerine prompt içinde tarif edilir; bu mod
    /// <c>json_schema</c> desteklemeyen sunucular içindir ve yeniden deneme yoluna daha sık düşülebilir.
    /// </summary>
    public bool UseJsonSchema { get; set; } = true;
}
