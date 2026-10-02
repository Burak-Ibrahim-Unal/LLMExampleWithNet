namespace SupportAssistant.API.Security;

/// <summary>
/// Soru ucunun hız sınırı ayarları; <c>RateLimiting</c> yapılandırma bölümünden bağlanır (ör.
/// <c>RateLimiting__QuestionsPerMinute</c> ortam değişkeni).
/// </summary>
/// <remarks>
/// Sınır istemci başına (IPv4 adresi ya da IPv6 adresinin /64 ağı; bkz. <see cref="ClientPartitionKey"/>), bir
/// dakikalık sabit pencereyle uygulanır ve yalnızca <c>POST /v1/questions</c> ucunu kapsar: her soru yerel modelde
/// saniyeler süren bir çağrı ve bir denetim kaydı demektir, sağlık ve doküman uçları ise ucuzdur. API bir ters vekil sunucunun (reverse proxy) arkasında çalışıyorsa bütün istekler vekilin IP'sinden gelir;
/// o durumda sınır vekilde uygulanmalı ya da <c>ForwardedHeaders</c> ara katmanı yalnızca güvenilen vekil için
/// yapılandırılmalıdır (README, Güvenlik).
/// </remarks>
public sealed class RateLimitingOptions
{
    /// <summary>Yapılandırmadaki bölüm adı.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Soru ucuna bağlanan hız sınırı politikasının adı.</summary>
    public const string QuestionsPolicy = "questions";

    /// <summary>
    /// Bir istemcinin (IP) dakikada gönderebileceği soru sayısı (varsayılan 30). 0 veya negatif değer sınırı kapatır.
    /// </summary>
    /// <remarks>
    /// 30, bir temsilcinin gerçekçi soru hızının (yerel modelde soru başına birkaç saniye) çok üstündedir; değerlendirme
    /// aracının 16 ve 12 soruluk setleri de sınıra takılmaz. Amaç meşru kullanımı kısıtlamak değil, tek bir istemcinin
    /// modeli kesintisiz meşgul etmesini önlemektir.
    /// </remarks>
    public int QuestionsPerMinute { get; set; } = 30;
}
