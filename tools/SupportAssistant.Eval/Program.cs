using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Knowledge.Application.Contracts;
using SupportAssistant.Eval;

// Değerlendirme aracı: değerlendirme sorularını çalışan bir API'ye sorar, report.md + results.json yazar.
// Entegrasyon testlerinden farkı: orada sahte bir dil modeliyle HTTP sözleşmesi sınanır; burada API gerçek dil modeli,
// gerçek embedding sunucusu ve gerçek bilgi tabanıyla, dışarıdan bir istemci gibi HTTP üzerinden ölçülür.
// Puanlama deterministiktir (EvalChecks); LLM-as-judge kullanılmaz, çünkü aynı küçük modelle yargılamak zayıf ve
// tekrarlanamaz olurdu. Kural tabanlı kontroller her koşuda aynı sonucu verir ve sorunun neden kaldığını açıkça gösterir.
// Çıkış kodları: 0 = bütün sorular geçti, 1 = en az bir soru kaldı, 2 = API'ye ulaşılamadı, 3 = geçersiz argüman.
// Böylece araç bir CI adımı olarak da kullanılabilir: kalan bir soru komutu başarısız kılar.

EvalOptions options;

// 1) Argümanlar. Hatalı kullanımda yığın izi yerine kullanım metnini içeren tek bir hata satırı basılır.
try
{
    options = EvalOptions.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 3;
}

// 2) Web varsayılanları (camelCase, büyük/küçük harfe duyarsız eşleşme) API'nin JSON biçimiyle aynıdır; soru dosyası,
//    istek gövdesi ve API yanıtları aynı ayarla okunup yazılır.
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// TODO(halüsinasyon-5): Halüsinasyon seti (eval/questions-hallucination.json) ve rapor tarafı ("desteksiz iddia oranı",
// HallucinationSignals) yapıldı. Kalan: aynı kontrollerin denetim kaydındaki (question_logs) gerçek yanıtlara düzenli
// uygulanmasıyla üretimdeki halüsinasyonun izlenmesi. Bkz. README, Halüsinasyon.

// Göreli yol depo köküne göre çözülür (bkz. EvalOptions.ResolveFromRepository). Dosyanın içeriği JSON null ise sıfır
// soruyla sessizce devam etmek yerine dosya yolunu gösteren bir hatayla durulur.
var questionsPath = EvalOptions.ResolveFromRepository(options.QuestionsPath);
var suite = JsonSerializer.Deserialize<EvalSuite>(await File.ReadAllTextAsync(questionsPath), json)
    ?? throw new InvalidOperationException($"Soru dosyası okunamadı: {questionsPath}");

// 3) Koşu boyunca tek bir HttpClient kullanılır. BaseAddress tek bir "/" ile bitmelidir; aksi hâlde "v1/health" gibi
//    göreli yollar birleştirilirken taban adresin son yol parçası (ör. bir ters vekil öneki) düşerdi.
//    Zaman aşımı 5 dk: varsayılan 100 sn, API'nin kendi LLM zaman aşımından (varsayılan 120 sn), yeniden denemesinden ve
//    düzeltme turundan kısadır; istemci erken koparsa API'nin döndüreceği 502/503 zarfı yerine anlamsız bir istemci
//    hatası görülürdü.
using var http = new HttpClient
{
    BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
    Timeout = TimeSpan.FromMinutes(5)
};

// 4) Önce sağlık ucu: API çalışmıyorsa her soruyu tek tek başarısız saymak yerine tek ve açık bir mesajla (kod 2)
//    durulur. Dönen durum (dil modeli, embedding modeli, arama modu) rapor başlığına yazılır; farklı yapılandırmalarla
//    alınan koşular (ör. düşünme modu açık/kapalı) ancak böyle karşılaştırılabilir.
SystemStatusDto? status;
IReadOnlyDictionary<string, string> documentTexts;

try
{
    status = (await http.GetFromJsonAsync<ApiEnvelope<SystemStatusDto>>("v1/health", json))?.Data;
    // Doküman metinleri bir kez alınır: "sayılar kaynakta" kontrolü yanıttaki her sayıyı atıf yapılan dokümanın
    // metniyle karşılaştırır. Metinler değerlendirilen API'den alındığı için kontrol, API'nin gerçekten indekslediği
    // içerikle yapılır.
    documentTexts = await LoadDocumentTextsAsync();
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
    var (statusCode, envelope, failure) = await AskAsync(question.Question);
    stopwatch.Stop();

    // Yalnızca HTTP 200, success=true ve veri taşıyan bir zarf değerlendirilir. Hata zarfı (400/502/503), JSON olmayan
    // bir gövde ya da başarısız istek, soruyu tek bir başarısız "yanıt" kontrolüyle kaydeder; HTTP kodu ve neden rapora
    // geçer. Altyapı hatası koşuyu durdurmaz ama asla "geçti" sayılmaz; özellikle cevapsız bir soruda sağlayıcı hatası
    // doğru bir "bilgi yok" reddi gibi görünemez. "Bilgi yok" reddi ise 200 + veri olarak geldiği için normal
    // kontrollerden geçer.
    IReadOnlyList<CheckResult> checks = statusCode == 200 && envelope is { Success: true, Data: { } answer }
        ? EvalChecks.Evaluate(question.Expect, answer, question.Question, documentTexts)
        : [new CheckResult("yanıt", false, failure ?? $"HTTP {statusCode}: {envelope?.Message}")];

    // Arama isabeti yanıttan ayrı ve iki modda ölçülür: beklenen doküman aramada hiç yoksa hata erişimdedir, varken yanıt
    // yanlışsa kapılarda ya da modeldedir. lexical ile hybrid arasındaki fark vektör aramasının katkısını gösterir.
    var lexical = await SearchAsync(question.Question, "lexical");
    var hybrid = await SearchAsync(question.Question, "hybrid");
    var result = new QuestionResult(
        question,
        statusCode,
        envelope?.Message ?? failure ?? string.Empty,
        envelope?.Data,
        checks,
        stopwatch.ElapsedMilliseconds,
        lexical is null ? null : EvalChecks.RetrievalHit(question.Expect, lexical),
        hybrid is null ? null : EvalChecks.RetrievalHit(question.Expect, hybrid));

    results.Add(result);
    // İlerleme satırı: dakikalar sürebilen bir koşuda her sorunun sonucu ve süresi anında görünür.
    Console.WriteLine($"{question.Id,-4} {(result.Passed ? "GEÇTİ" : "KALDI"),-6} {stopwatch.ElapsedMilliseconds,6} ms  {question.Question}");
}

// 6) --label verilmişse sonuçlar çıktı klasörünün alt klasörüne yazılır (ör. eval/results/thinking-on); farklı
//    yapılandırma koşuları varsayılan raporun üzerine yazmaz.
var outputDirectory = EvalOptions.ResolveFromRepository(options.Label is null ? options.OutputPath : Path.Combine(options.OutputPath, options.Label));
await ReportWriter.WriteAsync(outputDirectory, options, status, results);

// 7) Özet ve çıkış kodu: bir soru bile kaldıysa 1 döner; başarı oranı bu satırda ve raporda okunur. Halüsinasyon
//    sinyali çıkış kodunu ayrıca etkilemez: her sinyal kalan bir kontrolden geldiği için o soru zaten kalmıştır.
var passed = results.Count(result => result.Passed);
var hallucination = HallucinationSignals.Summarize(results);
Console.WriteLine($"\n{passed}/{results.Count} soru geçti. Halüsinasyon sinyali: {hallucination.Flagged}/{hallucination.Answered} yanıtta.");
Console.WriteLine($"Rapor: {Path.Combine(outputDirectory, "report.md")}");
return passed == results.Count ? 0 : 1;

// Yerel fonksiyon: bir soruyu POST /v1/questions ucuna sorar ve HTTP kodunu, zarfı ve (varsa) neden okunamadığını döndürür.
// İki tür arıza istisna olarak yukarı taşınmaz, sonuç olarak döner: isteğin kendisinin başarısız olması (bağlantı kopması,
// istemci zaman aşımı) ve gövdenin JSON olmaması (ör. işlenmemiş bir hatanın boş ya da HTML gövdesi). Böylece tek bir
// sorudaki altyapı arızası bütün koşuyu çökertmez, o soru kalmış olarak raporlanır.
async Task<(int StatusCode, ApiEnvelope<AnswerDto>? Envelope, string? Failure)> AskAsync(string text)
{
    try
    {
        using var response = await http.PostAsJsonAsync("v1/questions", new { question = text }, json);
        var code = (int)response.StatusCode;

        try
        {
            return (code, await response.Content.ReadFromJsonAsync<ApiEnvelope<AnswerDto>>(json), null);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return (code, null, $"HTTP {code}: yanıt gövdesi okunamadı ({exception.Message})");
        }
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
    {
        return (0, null, $"istek başarısız: {exception.Message}");
    }
}

// Yerel fonksiyon: arama sonuçlarının başındaki dokümanlar. Soruyu GET /v1/search ucuna verilen modla (lexical =
// yalnızca BM25, hybrid = BM25 + vektör) sorar ve sonuçlardaki farklı doküman kimliklerini döndürür. topK bilerek
// gönderilmez, sunucuya bırakılır: sunucunun varsayılanı modele verilen bölüm sayısıyla (Retrieval:TopK) aynıdır, böylece
// isabet modelin görebileceği kadar sonuç içinde ölçülür. Arama ucu sürüm çözümü uygulamaz; ölçülen şey ham erişimdir.
// Sorgu, Türkçe karakterler ve boşluklar için URL'de kaçışlanır. Arama başarısız olursa isabet ölçülemez: null döner,
// bir uyarı basılır ve raporda o soru için isabet "—" görünür; ölçülemeyen isabet ıska sayılmaz.
async Task<IReadOnlyCollection<string>?> SearchAsync(string query, string mode)
{
    try
    {
        var envelope = await http.GetFromJsonAsync<ApiEnvelope<SearchResultDto>>($"v1/search?q={Uri.EscapeDataString(query)}&mode={mode}", json);
        return envelope?.Data?.Hits.Select(hit => hit.DocumentId).Distinct().ToList() ?? [];
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
    {
        Console.Error.WriteLine($"Arama isabeti ölçülemedi ({mode}): {exception.Message}");
        return null;
    }
}

// Yerel fonksiyon: API'deki bütün dokümanların metnini kimliğe göre toplar (başlık, sürüm, yürürlük tarihi, bölüm yolları
// ve içerikler). Sürüm ve tarih de eklenir, çünkü doğru bir yanıt "2.0 sürümüne göre" ya da "2025-06-01 itibarıyla"
// diyebilir; bunlar kaynakta geçen sayılardır.
async Task<IReadOnlyDictionary<string, string>> LoadDocumentTextsAsync()
{
    var texts = new Dictionary<string, string>(StringComparer.Ordinal);
    var documents = (await http.GetFromJsonAsync<ApiEnvelope<List<DocumentSummaryDto>>>("v1/documents", json))?.Data ?? [];

    foreach (var summary in documents)
    {
        var detail = (await http.GetFromJsonAsync<ApiEnvelope<DocumentDetailDto>>($"v1/documents/{Uri.EscapeDataString(summary.Id)}", json))?.Data;

        if (detail is not null)
        {
            texts[detail.Id] = string.Join(
                "\n",
                [detail.Title, detail.Version, detail.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    .. detail.Sections.SelectMany(section => new[] { section.Section, section.Content })]);
        }
    }

    return texts;
}
