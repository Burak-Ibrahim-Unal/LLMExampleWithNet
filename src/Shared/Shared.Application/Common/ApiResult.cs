namespace Shared.Application.Common;

/// <summary>
/// Tüm uç noktaların kullandığı tek yanıt zarfı: <c>{ success, statusCode, message, data }</c>.
/// </summary>
/// <remarks>
/// Başarılı yanıt, iş hatası (ör. 400 boş soru, 404 doküman yok, 422 işlenemeyen bilgi tabanı, 503 indeks hazır değil)
/// ve dış servis hatası (502/503 dil modeli) aynı şekilde döner; istemci ve değerlendirme aracı her yanıtı tek bir
/// modelle okuyabilir. Handler'lar ve iş kuralları HTTP'yi bilmeden durum kodunu bu nesnede taşır; uç nokta yalnızca
/// <see cref="StatusCode"/> değerini HTTP durumu olarak gönderir. Exception yerine sonuç nesnesi kullanmak, beklenen
/// hataların (doğrulama, bulunamadı) normal kontrol akışında ele alınmasını sağlar. Yanıtlanamayan soru bir hata değil
/// geçerli bir iş sonucu olduğu için 200 ve <c>answerable=false</c> ile döner. Özellikler dışarıdan değiştirilemez
/// (<c>private init</c>); zarf yalnızca aşağıdaki fabrika metotlarıyla kurulur, böylece başarı bayrağı, mesaj ve durum
/// kodu tek noktada tutarlı biçimde belirlenir.
/// </remarks>
/// <typeparam name="T">Başarılı yanıtta <see cref="Data"/> içinde taşınan yükün tipi.</typeparam>
public sealed class ApiResult<T>
{
    /// <summary>İşlemin başarılı olup olmadığı; <see cref="Fail"/> ile üretilen sonuçlarda <c>false</c>.</summary>
    public bool Success { get; private init; }

    /// <summary>
    /// İnsan tarafından okunacak açıklama. Hatalarda genellikle <see cref="Messages"/> sınıfındaki merkezi Türkçe
    /// metinlerden biridir; istemciler ve testler hatayı bu metinle ayırt edebilir.
    /// </summary>
    public string Message { get; private init; } = string.Empty;

    /// <summary>
    /// Başarılı yanıtın yükü; hata sonuçlarında <c>default</c> (bu projedeki DTO'lar için JSON'da <c>null</c>).
    /// </summary>
    public T? Data { get; private init; }

    /// <summary>
    /// Yanıtın HTTP durum kodu. Uç noktalar bu değeri doğrudan HTTP durumu olarak gönderir; böylece gövdedeki kod ile
    /// HTTP durumu hiçbir zaman çelişmez.
    /// </summary>
    public int StatusCode { get; private init; }

    /// <summary>
    /// 200 OK başarılı sonucu üretir. Varsayılan mesaj "OK"dur; handler'lar anlam taşıyan durumlarda merkezi bir Türkçe
    /// mesaj verir (ör. yeniden indekslemede <c>Messages.Knowledge.Reindexed</c>, yanıtlanamayan soruda
    /// <c>Messages.Knowledge.NotEnoughInformation</c>).
    /// </summary>
    public static ApiResult<T> Ok(T data, string message = "OK") =>
        new()
        {
            Success = true,
            Message = message,
            Data = data,
            StatusCode = 200
        };

    /// <summary>
    /// 201 Created başarılı sonucu üretir. Şablondan gelir; mevcut uç noktaların hiçbiri yeni bir kaynak oluşturmadığı
    /// için (sorular yanıtlanır, dokümanlar dosyalardan türetilir) şu an kullanılmıyor.
    /// </summary>
    public static ApiResult<T> Created(T data, string message = "Created") =>
        new()
        {
            Success = true,
            Message = message,
            Data = data,
            StatusCode = 201
        };

    /// <summary>
    /// Başarısız sonucu verilen mesaj ve durum koduyla (varsayılan 400) üretir; <see cref="Data"/> boş kalır. İş
    /// kuralları ve handler'lar beklenen hataları exception fırlatmak yerine bununla döndürür: 400 geçersiz girdi,
    /// 404 doküman yok, 422 işlenemeyen bilgi tabanı, 502 geçersiz model çıktısı, 503 indeks hazır değil veya dil modeli
    /// erişilemez.
    /// </summary>
    public static ApiResult<T> Fail(string message, int statusCode = 400) =>
        new()
        {
            Success = false,
            Message = message,
            Data = default,
            StatusCode = statusCode
        };

    /// <summary>
    /// Aynı başarı bayrağı, mesaj ve yükle, yalnızca durum kodu farklı yeni bir zarf döndürür; nesne değişmez olduğu
    /// için mevcut sonuç değiştirilmez, kopyalanır. Şablondan gelen bir yardımcıdır; şu an hiçbir çağıranı yok.
    /// </summary>
    public ApiResult<T> WithStatus(int statusCode) =>
        new()
        {
            Success = Success,
            Message = Message,
            Data = Data,
            StatusCode = statusCode
        };
}
