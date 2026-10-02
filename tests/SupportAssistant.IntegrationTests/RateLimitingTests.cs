using System.Net;
using System.Net.Http.Json;
using Shared.Application.Common;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

/// <summary>
/// <c>POST /v1/questions</c> ucundaki istemci (IP) başına hız sınırını HTTP düzeyinde sınar. Testler dakikada yalnızca
/// <see cref="LowRateLimitApiFactory.QuestionsPerMinute"/> soruya izin veren ayrı bir fabrika kullanır; diğer test
/// sınıflarının fabrikası sınırı yüksek tutar ve bu testlerden etkilenmez.
/// </summary>
/// <remarks>
/// Her soru yerel modelde saniyeler süren bir çağrı ve bir denetim kaydı demektir; sınırsız bir uç, tek bir istemcinin
/// modeli meşgul edip diğer temsilcileri bekletmesine izin verirdi. Sınır yalnızca soru ucuna uygulanır: sağlık ve doküman
/// uçları ucuzdur ve izleme araçlarının bunlara sınırsız erişebilmesi gerekir.
/// </remarks>
public sealed class RateLimitingTests(LowRateLimitApiFactory factory) : IClassFixture<LowRateLimitApiFactory>
{
    /// <summary>
    /// Sınıra kadar olan soruların yanıtlandığını, sınırı aşan sorunun 429, <c>Retry-After</c> başlığı ve aynı
    /// <c>ApiResult</c> zarfıyla (başarısız, 429, Türkçe mesaj) reddedildiğini ve sınırın sağlık ucunu etkilemediğini
    /// doğrular.
    /// </summary>
    /// <remarks>
    /// Zarfın korunması istemcinin hata işleme kodunu tek tip tutar: 429 da diğer hatalar gibi <c>success</c>,
    /// <c>message</c> ve <c>statusCode</c> alanlarıyla okunur. <c>Retry-After</c>, istemcinin ne kadar bekleyeceğini tahmin
    /// etmek zorunda kalmamasını sağlar.
    /// </remarks>
    [Fact]
    public async Task Questions_beyond_the_per_minute_limit_are_rejected_with_429()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        for (var question = 0; question < LowRateLimitApiFactory.QuestionsPerMinute; question++)
        {
            using var allowed = await client.PostAsJsonAsync("/v1/questions", new { question = "İade süresi kaç gün?" }, cancellationToken);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var limited = await client.PostAsJsonAsync("/v1/questions", new { question = "İade süresi kaç gün?" }, cancellationToken);
        using var envelope = await ApiResponse.ReadAsync(limited, cancellationToken);

        envelope.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.ShouldNotBeNull();
        envelope.Success.ShouldBeFalse();
        envelope.Message.ShouldBe(Messages.Security.TooManyRequests);

        using var health = await client.GetAsync("/v1/health", cancellationToken);
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
