using System.Net;
using System.Net.Http.Json;
using Shouldly;
using SupportAssistant.IntegrationTests.Infrastructure;

namespace SupportAssistant.IntegrationTests;

public sealed class QuestionsEndpointTests(SupportAssistantApiFactory factory) : IClassFixture<SupportAssistantApiFactory>
{
    private async Task<ApiResponse> AskAsync(string question)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/questions", new { question }, cancellationToken);
        return await ApiResponse.ReadAsync(response, cancellationToken);
    }

    [Fact]
    public async Task An_answer_shows_its_source_section_and_how_the_current_version_was_chosen()
    {
        using var response = await AskAsync("İade süresi kaç gün?");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("answerable").GetBoolean().ShouldBeTrue();
        var source = response.Data.GetProperty("sources")[0];
        source.GetProperty("documentId").GetString().ShouldBe("iade-v2");
        source.GetProperty("version").GetString().ShouldBe("2.0");
        source.GetProperty("section").GetString().ShouldBe("2. İade Süresi");
        var resolution = response.Data.GetProperty("versionResolution");
        resolution.GetProperty("applied").GetBoolean().ShouldBeTrue();
        resolution.GetProperty("discarded")[0].GetProperty("documentId").GetString().ShouldBe("iade-v1");
    }

    [Fact]
    public async Task A_question_the_documents_do_not_cover_is_explicitly_refused()
    {
        using var response = await AskAsync("Apple HomeKit ile uyumlu mu?");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Data.GetProperty("answerable").GetBoolean().ShouldBeFalse();
        response.Data.GetProperty("refusalReason").GetString().ShouldBe("LowRelevance");
        response.Data.GetProperty("answer").GetString().ShouldBe("Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı.");
        response.Data.GetProperty("sources").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task An_empty_question_is_rejected()
    {
        using var response = await AskAsync(string.Empty);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Message.ShouldBe("Soru boş olamaz.");
    }
}
