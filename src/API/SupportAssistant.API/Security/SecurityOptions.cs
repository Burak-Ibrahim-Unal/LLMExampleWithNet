namespace SupportAssistant.API.Security;

/// <summary>
/// API'nin koruma ayarları; <c>Security</c> yapılandırma bölümünden bağlanır (ör. <c>Security__AdminApiKey</c> ortam
/// değişkeni).
/// </summary>
/// <remarks>
/// Anahtar gibi gizli değerler kaynak koda ve <c>appsettings</c> dosyalarına yazılmaz; yerelde git dışındaki
/// <c>.env</c> dosyasından, sunucuda ortam değişkeninden ya da bir gizli değer deposundan gelir.
/// </remarks>
public sealed class SecurityOptions
{
    /// <summary>Yapılandırmadaki bölüm adı.</summary>
    public const string SectionName = "Security";

    /// <summary>Yönetici anahtarının taşındığı HTTP başlığı.</summary>
    public const string AdminKeyHeader = "X-Admin-Key";

    /// <summary>
    /// Yönetici işlemlerinin (<c>POST /v1/documents/reindex</c>) beklediği anahtar; varsayılan boş.
    /// </summary>
    /// <remarks>
    /// Boşsa yönetici uçları kapalıdır (403): anahtarı unutmak ucu açık bırakmaz. Uzun ve rastgele bir değer
    /// kullanılmalıdır (ör. <c>openssl rand -hex 32</c>). Açılıştaki indeksleme HTTP'den geçmediği için bu ayardan
    /// etkilenmez.
    /// </remarks>
    public string AdminApiKey { get; set; } = string.Empty;
}
