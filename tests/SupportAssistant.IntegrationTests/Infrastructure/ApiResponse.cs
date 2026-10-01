using System.Net;
using System.Text.Json;

namespace SupportAssistant.IntegrationTests.Infrastructure;

/// <summary>Raw view of an ApiResult envelope, read as JSON so tests check the wire format clients see.</summary>
public sealed class ApiResponse : IDisposable
{
    private readonly JsonDocument _json;

    private ApiResponse(HttpStatusCode statusCode, JsonDocument json)
    {
        StatusCode = statusCode;
        _json = json;
    }

    public HttpStatusCode StatusCode { get; }

    public bool Success => _json.RootElement.GetProperty("success").GetBoolean();

    public string Message => _json.RootElement.GetProperty("message").GetString()!;

    public JsonElement Data => _json.RootElement.GetProperty("data");

    public static async Task<ApiResponse> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return new ApiResponse(response.StatusCode, json);
    }

    public void Dispose()
    {
        _json.Dispose();
    }
}
