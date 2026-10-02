using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Knowledge.Application.Contracts;
using SupportAssistant.Eval;

// Değerlendirme aracı: değerlendirme sorularını çalışan bir API'ye sorar, report.md + results.json yazar.
// Entegrasyon testlerinden farkı: orada sahte bir dil modeliyle HTTP sözleşmesi sınanır; burada API gerçek dil modeli,
// gerçek embedding sunucusu ve gerçek bilgi tabanıyla, dışarıdan bir istemci gibi HTTP üzerinden ölçülür.
// Puanlama deterministiktir (EvalChecks); LLM-as-judge kullanılmaz, çünkü aynı küçük modelle yargılamak zayıf ve
// tekrarlanamaz olurdu. Kural tabanlı kontroller her koşuda aynı sonucu verir ve sorunun neden kaldığını açıkça gösterir.
// Çıkış kodları: 0 = koşu tamamlandı (kalan soru olsa bile), 1 = geçersiz argüman, 2 = API'ye ulaşılamadı.

EvalOptions options;

// 1) Argümanlar. Hatalı kullanımda yığın izi yerine kullanım metnini içeren tek bir hata satırı basılır.
try
{
    options = EvalOptions.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

// 2) Web varsayılanları (camelCase, büyük/küçük harfe duyarsız eşleşme) API'nin JSON biçimiyle aynıdır; soru dosyası,
//    istek gövdesi ve API yanıtları aynı ayarla okunup yazılır.
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
// Göreli yol depo köküne göre çözülür (bkz. EvalOptions.ResolveFromRepository). Dosyanın içeriği JSON null ise sıfır
// soruyla sessizce devam etmek yerine dosya yolunu gösteren bir hatayla durulur.
var questionsPath = EvalOptions.ResolveFromRepository(options.QuestionsPath);
var suite = JsonSerializer.Deserialize<EvalSuite>(await File.ReadAllTextAsync(questionsPath), json)
    ?? throw new InvalidOperationException($"Soru dosyası okunamadı: {questionsPath}");

// 3) Koşu boyunca tek bir HttpClient kullanılır. BaseAddress tek bir "/" ile bitmelidir; aksi hâlde "v1/health" gibi
//    göreli yollar birleştirilirken taban adresin son yol parçası (ör. bir ters vekil öneki) düşerdi.
//    Zaman aşımı 5 dk: varsayılan 100 sn, API'nin kendi LLM zaman aşımından (varsayılan 120 sn) ve yeniden denemesinden
//    kısadır; istemci erken koparsa API'nin döndüreceği 502/503 zarfı yerine anlamsız bir istemci hatası görülürdü.
using var http = new HttpClient
{
    BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
    Timeout = TimeSpan.FromMinutes(5)
};

// 4) Önce sağlık ucu: API çalışmıyorsa her soruyu tek tek başarısız saymak yerine tek ve açık bir mesajla (kod 2)
//    durulur. Dönen durum (dil modeli, embedding modeli, arama modu) rapor başlığına yazılır; farklı yapılandırmalarla
//    alınan koşular (ör. düşünme modu açık/kapalı) ancak böyle karşılaştırılabilir.
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

// Koşunun hangi yapılandırmaya karşı yapıldığı konsolda da en başta görünür.
Console.WriteLine($"API: {options.BaseUrl} | LLM: {status?.Llm.Model} | embedding: {status?.Embeddings.Model} | arama: {status?.Index.RetrievalMode} | {suite.Questions.Count} soru");

// 5) Sorular sırayla sorulur; böylece her ölçülen süre tek bir isteğe aittir (tek slotlu bir model sunucusunda paralel
//    istekler birbirini bekleyip süreleri şişirirdi).
var results = new List<QuestionResult>();

foreach (var question in suite.Questions)
{
    // Süre, istemcinin gördüğü uçtan uca gecikmedir (HTTP + arama + model + doğrulama). Kronometre yanıt okunur okunmaz
    // durur; aşağıdaki iki arama çağrısı bu süreye girmez.
    var stopwatch = Stopwatch.StartNew();
    using var response = await http.PostAsJsonAsync("v1/questions", new { question = question.Question }, json);
    var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AnswerDto>>(json);
    stopwatch.Stop();

    // Veri yoksa (400/502/503 gibi bir hata zarfı) soru tek bir başarısız "yanıt" kontrolüyle kaydedilir ve HTTP kodu ile
    // mesaj rapora geçer: altyapı hatası koşuyu durdurmaz ama asla "geçti" sayılmaz. "Bilgi yok" reddi ise 200 + veri
    // olarak geldiği için normal kontrollerden geçer.
    IReadOnlyList<CheckResult> checks = envelope?.Data is { } answer
        ? EvalChecks.Evaluate(question.Expect, answer)
        : [new CheckResult("yanıt", false, $"HTTP {(int)response.StatusCode}: {envelope?.Message}")];

    // Arama isabeti yanıttan ayrı ve iki modda ölçülür: beklenen doküman aramada hiç yoksa hata erişimdedir, varken yanıt
    // yanlışsa kapılarda ya da modeldedir. lexical ile hybrid arasındaki fark vektör aramasının katkısını gösterir.
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
    // İlerleme satırı: dakikalar sürebilen bir koşuda her sorunun sonucu ve süresi anında görünür.
    Console.WriteLine($"{question.Id,-4} {(result.Passed ? "GEÇTİ" : "KALDI"),-6} {stopwatch.ElapsedMilliseconds,6} ms  {question.Question}");
}

// 6) --label verilmişse sonuçlar çıktı klasörünün alt klasörüne yazılır (ör. eval/results/thinking-on); farklı
//    yapılandırma koşuları varsayılan raporun üzerine yazmaz.
var outputDirectory = EvalOptions.ResolveFromRepository(options.Label is null ? options.OutputPath : Path.Combine(options.OutputPath, options.Label));
await ReportWriter.WriteAsync(outputDirectory, options, status, results);

// 7) Özet. Kalan soru olsa bile çıkış kodu 0'dır; başarı oranı bu satırda ve raporda okunur.
Console.WriteLine($"\n{results.Count(result => result.Passed)}/{results.Count} soru geçti. Rapor: {Path.Combine(outputDirectory, "report.md")}");
return 0;

// Yerel fonksiyon: arama sonuçlarının başındaki dokümanlar. Soruyu GET /v1/search ucuna verilen modla (lexical =
// yalnızca BM25, hybrid = BM25 + vektör) sorar ve sonuçlardaki farklı doküman kimliklerini döndürür. topK bilerek
// gönderilmez, sunucuya bırakılır: sunucunun varsayılanı modele verilen bölüm sayısıyla (Retrieval:TopK) aynıdır, böylece
// isabet modelin görebileceği kadar sonuç içinde ölçülür. Arama ucu sürüm çözümü uygulamaz; ölçülen şey ham erişimdir.
// Sorgu, Türkçe karakterler ve boşluklar için URL'de kaçışlanır.
async Task<IReadOnlyCollection<string>> SearchAsync(string query, string mode)
{
    var envelope = await http.GetFromJsonAsync<ApiEnvelope<SearchResultDto>>($"v1/search?q={Uri.EscapeDataString(query)}&mode={mode}", json);
    return envelope?.Data?.Hits.Select(hit => hit.DocumentId).Distinct().ToList() ?? [];
}
