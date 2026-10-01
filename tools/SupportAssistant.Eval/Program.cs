using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Knowledge.Application.Contracts;
using SupportAssistant.Eval;

// Runs the evaluation questions against a running API and writes report.md + results.json.

EvalOptions options;

try
{
    options = EvalOptions.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var questionsPath = EvalOptions.ResolveFromRepository(options.QuestionsPath);
var suite = JsonSerializer.Deserialize<EvalSuite>(await File.ReadAllTextAsync(questionsPath), json)
    ?? throw new InvalidOperationException($"Soru dosyası okunamadı: {questionsPath}");

using var http = new HttpClient
{
    BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
    Timeout = TimeSpan.FromMinutes(5)
};

SystemStatusDto? status;

try
{
    status = (await http.GetFromJsonAsync<ApiEnvelope<SystemStatusDto>>("v1/health", json))?.Data;
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"API'ye ulaşılamadı ({options.BaseUrl}): {exception.Message}");
    return 2;
}

Console.WriteLine($"API: {options.BaseUrl} | LLM: {status?.Llm.Model} | embedding: {status?.Embeddings.Model} | arama: {status?.Index.RetrievalMode} | {suite.Questions.Count} soru");

var results = new List<QuestionResult>();

foreach (var question in suite.Questions)
{
    var stopwatch = Stopwatch.StartNew();
    using var response = await http.PostAsJsonAsync("v1/questions", new { question = question.Question }, json);
    var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AnswerDto>>(json);
    stopwatch.Stop();

    IReadOnlyList<CheckResult> checks = envelope?.Data is { } answer
        ? EvalChecks.Evaluate(question.Expect, answer)
        : [new CheckResult("yanıt", false, $"HTTP {(int)response.StatusCode}: {envelope?.Message}")];

    var result = new QuestionResult(
        question,
        (int)response.StatusCode,
        envelope?.Message ?? string.Empty,
        envelope?.Data,
        checks,
        stopwatch.ElapsedMilliseconds,
        EvalChecks.RetrievalHit(question.Expect, await SearchAsync(question.Question, "lexical")),
        EvalChecks.RetrievalHit(question.Expect, await SearchAsync(question.Question, "hybrid")));

    results.Add(result);
    Console.WriteLine($"{question.Id,-4} {(result.Passed ? "GEÇTİ" : "KALDI"),-6} {stopwatch.ElapsedMilliseconds,6} ms  {question.Question}");
}

var outputDirectory = EvalOptions.ResolveFromRepository(options.Label is null ? options.OutputPath : Path.Combine(options.OutputPath, options.Label));
await ReportWriter.WriteAsync(outputDirectory, options, status, results);

Console.WriteLine($"\n{results.Count(result => result.Passed)}/{results.Count} soru geçti. Rapor: {Path.Combine(outputDirectory, "report.md")}");
return 0;

// Documents among the top search results; topK is left to the server so it matches the number of sections the model receives.
async Task<IReadOnlyCollection<string>> SearchAsync(string query, string mode)
{
    var envelope = await http.GetFromJsonAsync<ApiEnvelope<SearchResultDto>>($"v1/search?q={Uri.EscapeDataString(query)}&mode={mode}", json);
    return envelope?.Data?.Hits.Select(hit => hit.DocumentId).Distinct().ToList() ?? [];
}
