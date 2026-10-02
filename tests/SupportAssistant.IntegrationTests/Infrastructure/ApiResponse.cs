using System.Net;
using System.Text.Json;

namespace SupportAssistant.IntegrationTests.Infrastructure;

/// <summary>
/// API'nin <c>ApiResult</c> zarfının (<c>success</c>, <c>statusCode</c>, <c>message</c>, <c>data</c>) ham görünümü.
/// Yanıt JSON olarak okunur; böylece testler istemcilerin gördüğü tel formatını (wire format) denetler.
/// </summary>
/// <remarks>
/// Yanıtı sunucunun kendi tiplerine geri deserialize etmek alan adı veya şekil değişikliklerini gizleyebilirdi;
/// özelliklere JSON'daki camelCase adlarıyla doğrudan erişmek, sözleşme bozulduğunda testlerin kırılmasını sağlar.
/// (<c>ApiResult&lt;T&gt;</c>'nin setter'ları <c>private init</c> olduğundan System.Text.Json onu zaten dolduramaz.)
/// </remarks>
public sealed class ApiResponse : IDisposable
{
    private readonly JsonDocument _json;

    /// <summary>
    /// Örnekler yalnızca <see cref="ReadAsync"/> ile oluşturulur; HTTP durum kodu ve ayrıştırılmış gövde birlikte tutulur.
    /// </summary>
    private ApiResponse(HttpStatusCode statusCode, JsonDocument json)
    {
        StatusCode = statusCode;
        _json = json;
    }

    /// <summary>
    /// Yanıtın HTTP durum kodu. Uçlar yanıtı zarftaki <c>statusCode</c> ile gönderdiği için testler doğru kodun
    /// (400, 404, 422, 503…) gerçekten HTTP katmanına yansıdığını buradan doğrular.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Zarfın <c>success</c> alanı; hata yanıtlarının da aynı zarf içinde döndüğünü doğrulamaya yarar.</summary>
    public bool Success => _json.RootElement.GetProperty("success").GetBoolean();

    /// <summary>Zarfın <c>message</c> alanı; testler kullanıcıya gösterilen Türkçe metni birebir karşılaştırır.</summary>
    public string Message => _json.RootElement.GetProperty("message").GetString()!;

    /// <summary>Zarfın <c>data</c> alanı. Hata yanıtlarında da bulunur; değeri o zaman JSON <c>null</c> olur.</summary>
    public JsonElement Data => _json.RootElement.GetProperty("data");

    /// <summary>
    /// Yanıt gövdesini okuyup JSON olarak ayrıştırır ve durum koduyla birlikte sarar. Gövde JSON değilse (ör. zarfsız bir
    /// çerçeve hata sayfası) ayrıştırma başarısız olur; bu da "her yanıt zarf içinde döner" beklentisini kendiliğinden
    /// denetler.
    /// </summary>
    public static async Task<ApiResponse> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return new ApiResponse(response.StatusCode, json);
    }

    /// <summary>
    /// <see cref="JsonDocument"/> belleği havuzdan kiraladığı için serbest bırakılmalıdır; testler <c>using</c> ile kullanır.
    /// </summary>
    public void Dispose()
    {
        _json.Dispose();
    }
}
