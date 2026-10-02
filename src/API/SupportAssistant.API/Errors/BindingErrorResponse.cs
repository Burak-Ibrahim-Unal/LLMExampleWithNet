using System.Globalization;
using FluentValidation.Results;
using Shared.Application.Common;

namespace SupportAssistant.API.Errors;

/// <summary>
/// İstek bağlanırken oluşan hataları (geçersiz JSON gövdesi, beklenen türe çevrilemeyen bir alan) diğer bütün hatalarla
/// aynı <see cref="ApiResult{T}"/> zarfına ve Türkçe mesaja çevirir. FastEndpoints'in genel hata üreticisi olarak
/// <c>Program.cs</c>'te bağlanır.
/// </summary>
/// <remarks>
/// <para>
/// Bu hatalar uç noktaya ve iş kurallarına ulaşmadan, çerçevenin istek bağlama adımında oluşur. Üretici bağlanmadan önce
/// çerçeve kendi İngilizce varsayılan biçimini döndürüyordu (<c>{"statusCode":400,"message":"One or more errors
/// occurred!","errors":{…}}</c>); canlı API denemesinde bulundu. İstemcinin hata işleme kodu tek bir biçime
/// güvenebilmelidir.
/// </para>
/// <para>
/// Mesaj, sorunlu alanları adıyla söyler (ör. <c>topK</c>, <c>question</c>); çerçevenin ayrıntılı İngilizce iletisi
/// (System.Text.Json hata metni) istemciye taşınmaz. Gövde hiç JSON olarak okunamadıysa alan adı yoktur ve genel mesaj
/// döner. Uygulama FastEndpoints doğrulayıcısı (<c>Validator</c>) kullanmadığı için bu üretici yalnızca bağlama
/// hatalarında çalışır; iş kuralı hataları zaten zarfla döner.
/// </para>
/// </remarks>
public static class BindingErrorResponse
{
    /// <summary>
    /// Belirli bir alana ait olmayan hata anahtarları: çerçevenin seri hâle getirme hataları ve genel hatalar için
    /// kullandığı adlar. Bunlar mesajdaki alan listesine girmez.
    /// </summary>
    private static readonly HashSet<string> GeneralErrorKeys = new(StringComparer.OrdinalIgnoreCase) { "serializerErrors", "GeneralErrors" };

    /// <summary>
    /// Çerçevenin topladığı bağlama hatalarından zarflı hata yanıtını üretir: başarısız, verilen durum kodu, sorunlu alanları
    /// sayan Türkçe mesaj ve boş veri.
    /// </summary>
    /// <param name="failures">Çerçevenin topladığı hatalar; her biri bir alan adı ve İngilizce bir ileti taşır.</param>
    /// <param name="context">İsteğin HTTP bağlamı; çerçevenin imzası gereği vardır, kullanılmaz.</param>
    /// <param name="statusCode">Çerçevenin seçtiği durum kodu (bağlama hatalarında 400).</param>
    public static object Create(List<ValidationFailure> failures, HttpContext context, int statusCode)
    {
        var fields = failures
            .Select(failure => failure.PropertyName)
            .Where(name => !string.IsNullOrWhiteSpace(name) && !GeneralErrorKeys.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var message = fields.Count == 0
            ? Messages.Request.Unreadable
            : string.Format(CultureInfo.InvariantCulture, Messages.Request.UnreadableFields, string.Join(", ", fields));

        return ApiResult<object>.Fail(message, statusCode);
    }
}
