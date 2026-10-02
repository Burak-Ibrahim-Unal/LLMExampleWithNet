using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Shared.Application.Common;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

/// <summary>
/// API genelindeki HTTP korumalarını sınar: her yanıttaki güvenlik başlıkları ve istek gövdesi boyut sınırı.
/// </summary>
/// <remarks>
/// Bu korumalar tek bir uca değil bütün API'ye uygulanır; bu yüzden hem başarılı hem hatalı yanıtlarda, hem de uç
/// noktaya hiç ulaşmayan reddetmelerde (413) geçerli olmaları beklenir.
/// </remarks>
public sealed class SecurityHeadersTests(SupportAssistantApiFactory factory) : IClassFixture<SupportAssistantApiFactory>
{
    /// <summary>
    /// Başarılı (sağlık ucu) ve hatalı (bilinmeyen doküman, 404) yanıtların üç güvenlik başlığını da taşıdığını doğrular:
    /// <c>X-Content-Type-Options: nosniff</c>, <c>X-Frame-Options: DENY</c> ve <c>Referrer-Policy: no-referrer</c>.
    /// </summary>
    /// <remarks>
    /// <c>nosniff</c> tarayıcının JSON yanıtını başka bir içerik türü (ör. betik) sanmasını, <c>DENY</c> Scalar arayüzünün
    /// başka bir sitede çerçeve içine alınmasını (clickjacking), <c>no-referrer</c> ise adreslerin üçüncü taraflara
    /// sızmasını önler. Başlıkların hata yanıtlarında da bulunması gerekir; saldırgan çoğu zaman hata yollarını dener.
    /// </remarks>
    [Theory]
    [InlineData("/v1/health")]
    [InlineData("/v1/documents/yok")]
    public async Task Every_response_carries_the_security_headers(string url)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url, cancellationToken);

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
    }

    /// <summary>
    /// Varsayılan sınırı (16 KB) aşan bir istek gövdesinin uç noktaya ulaşmadan 413, <c>ApiResult</c> zarfı, sınırı
    /// söyleyen Türkçe mesaj ve güvenlik başlıklarıyla reddedildiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Bir destek sorusu en fazla 500 karakterdir; iş kuralı bunu zaten 400 ile reddeder, ama ancak gövde okunup JSON
    /// ayrıştırıldıktan sonra. Sınır, megabaytlarca veriyi okumadan ve belleğe almadan reddeder. Gövde yalnızca
    /// <c>Content-Length</c> başlığına bakılarak reddedilir; istemci başlığı göndermezse sunucunun gövde sınırı devreye
    /// girer.
    /// </remarks>
    [Fact]
    public async Task An_oversized_request_body_is_rejected_with_413()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var raw = await client.PostAsJsonAsync("/v1/questions", new { question = new string('a', 20_000) }, cancellationToken);
        using var response = await ApiResponse.ReadAsync(raw, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        response.Success.ShouldBeFalse();
        response.Message.ShouldBe(string.Format(CultureInfo.InvariantCulture, Messages.Security.RequestTooLarge, 16_384));
        raw.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }
}
