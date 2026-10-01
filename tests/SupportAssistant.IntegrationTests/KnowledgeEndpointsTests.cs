using System.Net;
using System.Text.Json;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

public sealed class KnowledgeEndpointsTests(SupportAssistantApiFactory factory) : IClassFixture<SupportAssistantApiFactory>
{
    private async Task<ApiResponse> SendAsync(HttpMethod method, string url)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(method, url), cancellationToken);
        return await ApiResponse.ReadAsync(response, cancellationToken);
    }

    [Fact]
    public async Task Documents_are_listed_with_their_version_metadata()
    {
        using var response = await SendAsync(HttpMethod.Get, "/v1/documents");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetArrayLength().ShouldBe(3);
        var current = response.Data.EnumerateArray().Single(document => document.GetProperty("id").GetString() == "iade-v2");
        current.GetProperty("documentKey").GetString().ShouldBe("iade");
        current.GetProperty("version").GetString().ShouldBe("2.0");
        current.GetProperty("effectiveDate").GetString().ShouldBe("2025-06-01");
        current.GetProperty("status").GetString().ShouldBe("active");
        current.GetProperty("category").GetString().ShouldBe("politika");
        current.GetProperty("sectionCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Document_details_contain_each_section_in_order()
    {
        using var response = await SendAsync(HttpMethod.Get, "/v1/documents/iade-v2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("sections").EnumerateArray()
            .Select(section => section.GetProperty("section").GetString())
            .ShouldBe(["2. İade Süresi", "5. İade Kargo Ücreti"]);
    }

    [Fact]
    public async Task An_unknown_document_returns_404_in_the_envelope()
    {
        using var response = await SendAsync(HttpMethod.Get, "/v1/documents/yok");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Success.ShouldBeFalse();
        response.Message.ShouldBe("Doküman bulunamadı.");
    }

    [Fact]
    public async Task Search_returns_ranked_sections_with_their_scores()
    {
        using var response = await SendAsync(HttpMethod.Get, "/v1/search?q=iade%20s%C3%BCresi&topK=2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("retrievalMode").GetString().ShouldBe("lexical");
        var hits = response.Data.GetProperty("hits").EnumerateArray().ToList();
        hits.Count.ShouldBe(2);
        hits.ShouldAllBe(hit => hit.GetProperty("section").GetString() == "2. İade Süresi");
        hits.Select(hit => hit.GetProperty("documentId").GetString()).ShouldBe(["iade-v1", "iade-v2"], ignoreOrder: true);
        hits[0].GetProperty("lexicalCoverage").GetDouble().ShouldBe(1.0, 1e-9);
    }

    [Theory]
    [InlineData("/v1/search?q=", "Arama ifadesi boş olamaz.")]
    [InlineData("/v1/search?q=iade&topK=0", "topK 1 ile 20 arasında olmalıdır.")]
    public async Task Search_rejects_invalid_input(string url, string expectedMessage)
    {
        using var response = await SendAsync(HttpMethod.Get, url);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Message.ShouldBe(expectedMessage);
    }

    [Fact]
    public async Task Reindexing_an_unchanged_knowledge_base_keeps_every_document()
    {
        using var response = await SendAsync(HttpMethod.Post, "/v1/documents/reindex");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("documents").GetInt32().ShouldBe(3);
        response.Data.GetProperty("unchanged").GetInt32().ShouldBe(3);
        response.Data.GetProperty("added").GetInt32().ShouldBe(0);
    }
}

public sealed class MissingKnowledgeBaseTests(MissingKnowledgeBaseApiFactory factory) : IClassFixture<MissingKnowledgeBaseApiFactory>
{
    [Fact]
    public async Task Search_reports_service_unavailable_while_the_index_is_not_built()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await ApiResponse.ReadAsync(await client.GetAsync("/v1/search?q=iade", cancellationToken), cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Reindex_explains_why_the_knowledge_base_cannot_be_read()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await ApiResponse.ReadAsync(await client.PostAsync("/v1/documents/reindex", null, cancellationToken), cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Message.ShouldContain("Bilgi tabanı klasörü bulunamadı");
        response.Data.ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
