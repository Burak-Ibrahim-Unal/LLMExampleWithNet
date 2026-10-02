using Shared.Kernel.Abstractions;

namespace Knowledge.Domain.Entities;

/// <summary>
/// Yanıtlanan (veya geri çevrilen) bir sorunun denetim kaydı; istemciye gönderilen yanıtın tamamı da dahil.
/// </summary>
/// <remarks>
/// Hem denetim (hangi soruya hangi kaynaklarla ne cevap verildi) hem değerlendirme ve hata ayıklama (hangi kapıda geri
/// çevrildi, ne kadar sürdü) için tutulur. Yalnızca iş sonucu üreten sorular kaydedilir, yani 200 ile dönen yanıtlar ve
/// <c>answerable=false</c> geri çevirmeler; doğrulama hataları (400) ve indeks/dil modeli hataları (502/503) kaydedilmez.
/// Kayıt değişmezdir: tüm alanlar private set'tir ve yalnızca constructor ile atanır.
/// </remarks>
public sealed class QuestionLog : EntityBase
{
    /// <summary>Yalnızca EF Core'un veritabanı satırından nesne oluşturması (materialization) için.</summary>
    private QuestionLog()
    {
    }

    /// <summary>
    /// Bir yanıt veya geri çevirme için denetim kaydı oluşturur. Değerler istemciye dönen yanıttan (<c>AnswerDto</c>)
    /// alınır; yanıtın tamamı <paramref name="responseJson"/> olarak saklanır.
    /// </summary>
    public QuestionLog(string question, bool answerable, string refusalReason, string model, long latencyMs, string responseJson)
    {
        Question = question;
        Answerable = answerable;
        RefusalReason = refusalReason;
        Model = model;
        LatencyMs = latencyMs;
        ResponseJson = responseJson;
    }

    /// <summary>Kullanıcının sorusu, baştaki ve sondaki boşluklar kırpılmış hâliyle.</summary>
    public string Question { get; private set; } = string.Empty;

    /// <summary>Soru kaynaklara dayanarak yanıtlandıysa <c>true</c>; herhangi bir kapıda geri çevrildiyse <c>false</c>.</summary>
    public bool Answerable { get; private set; }

    /// <summary>
    /// Geri çevirme gerekçesi (<c>LowRelevance</c>, <c>NoSourceInEffect</c>, <c>ModelInsufficientContext</c>,
    /// <c>NoValidCitations</c>); yanıtlanan sorularda boş metin. Ayrı bir sütun olması, kapıların ne sıklıkla devreye
    /// girdiğini JSON'u açmadan sorgulamayı mümkün kılar.
    /// </summary>
    public string RefusalReason { get; private set; } = string.Empty;

    /// <summary>
    /// Yanıtı üreten dil modelinin yapılandırmadaki adı; model çağrılmadan verilen geri çevirmelerde (<c>LowRelevance</c>,
    /// <c>NoSourceInEffect</c>) boş. Sunucunun döndürdüğü model kimliği kullanılmaz, çünkü llama.cpp oraya yerel model
    /// dosyasının yolunu yazar ve bu, makineye ait ayrıntıları sızdırırdı.
    /// </summary>
    public string Model { get; private set; } = string.Empty;

    /// <summary>
    /// Milisaniye cinsinden işlem süresi: iş kuralları geçildikten sonra başlar; aramayı ve (çağrıldıysa) dil modeli
    /// adımını kapsar.
    /// </summary>
    public long LatencyMs { get; private set; }

    /// <summary>
    /// İstemciye gönderilen yanıtın tamamı, camelCase JSON olarak; Türkçe karakterler kaçış dizisine çevrilmeden,
    /// okunabilir biçimde saklanır. Böylece kayıt, o an gönderilen yanıtı kaynaklar ve tanılama bilgileriyle birlikte
    /// aynen korur.
    /// </summary>
    public string ResponseJson { get; private set; } = string.Empty;
}
