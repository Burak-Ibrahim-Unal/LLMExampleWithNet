using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;
using Knowledge.Application.Security;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Llm;

/// <summary>
/// <see cref="IGroundedAnswerGenerator"/> portunun gerçek adaptörü: OpenAI-uyumlu bir sohbet ucunu
/// Microsoft.Extensions.AI üzerinden çağırır ve yanıtı JSON şemasıyla kısıtlanmış yapıda (<see cref="AnswerPayload"/>)
/// ister. llama.cpp şemayı bir grammar'a çevirdiği için yanıt normalde her zaman ayrıştırılır; yine de ayrıştırılamayan
/// (ör. token sınırında yarım kalan ya da şema desteği olmayan bir sunucudan gelen), zorunlu bir alanı eksik olan
/// (<c>{}</c> gibi), listelerinde <c>null</c> öğe bulunan ya da <c>answerable=true</c> olduğu hâlde yanıt metni boş olan
/// bir çıktı, çağıranın verdiği deneme bütçesi elverdiği sürece düzeltici bir mesajla yeniden denenir. Böylece bozuk bir
/// çıktı hiçbir zaman modelin "bilgi yok" kararı sanılmaz.
/// </summary>
/// <remarks>
/// Hatalar Application katmanının tanıdığı tek bir istisna türüne çevrilir: uca ulaşılamaması, zaman aşımı veya uçtan
/// dönen bir hata <see cref="AnswerGenerationFailure.Unavailable"/> (HTTP 503), deneme bütçesi boyunca geçerli yapı
/// alınamaması <see cref="AnswerGenerationFailure.InvalidOutput"/> (HTTP 502) olur. Böylece bu teknik arızalar
/// "bilgi yok" diye geçiştirilmez ve Application katmanı OpenAI SDK'sını ya da HTTP ayrıntılarını tanımak zorunda
/// kalmaz.
/// </remarks>
public sealed class OpenAiCompatibleAnswerGenerator(
    IChatClient chatClient,
    IOptions<LlmOptions> options,
    ILogger<OpenAiCompatibleAnswerGenerator> logger) : IGroundedAnswerGenerator
{
    /// <summary>
    /// Şema üretimi ve yanıt ayrıştırması için JSON ayarları. Temel alınan <c>AIJsonUtilities.DefaultOptions</c>
    /// camelCase alan adları üretir; bu adlar sistem prompt'unda geçen adlarla (answerable, missingInformation…) aynıdır.
    /// <c>RespectNullableAnnotations</c> sayesinde null olamayan özellikler üretilen şemada da null olamaz kalır
    /// (<c>["string","null"]</c> birleşimleri oluşmaz) ve llama.cpp'nin şemadan ürettiği grammar sade kalır. Aynı ayar
    /// ayrıştırmada da uygulanır: null olamayan bir alana açıkça yazılmış null, geçersiz çıktı sayılır ve yeniden
    /// denemeyi tetikler.
    /// </summary>
    private static readonly JsonSerializerOptions SchemaOptions = new(AIJsonUtilities.DefaultOptions)
    {
        RespectNullableAnnotations = true
    };

    /// <summary>
    /// Her zaman <c>true</c>: bu adaptör yalnızca <c>Llm:BaseUrl</c> doluyken oluşturulur. "Yapılandırılmış" olmak
    /// "erişilebilir" olmak demek değildir; sağlık uç noktası ağ çağrısı yapmaz, erişim sorunu modele ulaşması gereken
    /// ilk soruda 503 olarak görünür.
    /// </summary>
    public bool IsConfigured => true;

    /// <summary>Yapılandırılmış model adı (<see cref="LlmOptions.ChatModel"/>); sağlık uç noktasında gösterilir.</summary>
    public string ModelName => options.Value.ChatModel;

    /// <summary>
    /// Soruyu ve C1..Cn etiketli kaynakları modele gönderir, yapılandırılmış yanıtı ayrıştırıp
    /// <see cref="GeneratedAnswer"/> kaydına çevirir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// İstek, Türkçe sistem prompt'u (<see cref="AnswerPrompt.System"/>) ile kaynakları ve soruyu içeren kullanıcı
    /// mesajından oluşur; model adı, sıcaklık, seed ve token sınırı yapılandırmadan gelir. Yanıt
    /// <c>GetResponseAsync&lt;T&gt;</c> ile <see cref="AnswerPayload"/> şemasına göre istenir;
    /// <see cref="LlmOptions.UseJsonSchema"/> şemanın <c>response_format</c> ile mi yoksa prompt içinde mi
    /// iletileceğini belirler.
    /// </para>
    /// <para>
    /// Geçersiz bir çıktıda modelin kendi yanıtı asistan mesajı olarak, ardından
    /// <see cref="AnswerPrompt.RetryInstruction"/> kullanıcı mesajı olarak konuşmaya eklenir ve yanıt bir kez daha
    /// istenir. İsteği aynen tekrarlamak yerine düzeltici talimat eklenir, çünkü sıcaklık 0 ve sabit seed ile aynı
    /// istek büyük olasılıkla aynı hatalı çıktıyı üretirdi.
    /// </para>
    /// <para>
    /// Ağ veya uç hataları <see cref="AnswerGenerationFailure.Unavailable"/>, deneme bütçesi
    /// (<paramref name="maxAttempts"/>) bittiğinde hâlâ geçersiz olan çıktı
    /// <see cref="AnswerGenerationFailure.InvalidOutput"/> nedeniyle <see cref="AnswerGenerationException"/> olarak
    /// fırlatılır. Çağıranın kendi iptal isteği ise sarmalanmaz, iptal olarak yukarı taşınır.
    /// </para>
    /// <para>
    /// Deneme bütçesi çağırandan gelir: handler soru başına tek bir model çağrısı bütçesi tutar ve buraya yalnızca
    /// kalanını verir. Yapılan gerçek istek sayısı yanıtta <see cref="GeneratedAnswer.Attempts"/> olarak döner; böylece
    /// buradaki şema yeniden denemesi bütçeye sayılır ve tanılamada görünür.
    /// </para>
    /// <para>
    /// <paramref name="feedback"/> handler'ın düzeltme turuna aittir ve buradaki şema yeniden denemesinden ayrıdır:
    /// şema denemesi ayrıştırılamayan çıktıyı onarır, handler'ın turu ise ayrıştırılmış ama kabul edilmeyen yanıtı
    /// (doğrulanamayan alıntılar) yeniden ister. Geri bildirim kullanıcı mesajına eklenir
    /// (<see cref="AnswerPrompt.BuildUserMessage"/>).
    /// </para>
    /// </remarks>
    public async Task<GeneratedAnswer> GenerateAsync(
        string question,
        IReadOnlyList<ContextChunk> context,
        AnswerFeedback? feedback = null,
        int maxAttempts = 2,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        // En az bir istek her zaman yapılır; sıfır ya da negatif bir bütçe çağıranın hatasıdır, sessizce yanıtsız kalınmaz.
        var attemptBudget = Math.Max(1, maxAttempts);
        // Token kullanımı bütün denemeler boyunca toplanır: ayrıştırılamayan bir yanıtın token'ları da harcanmış maliyettir.
        long? inputTokens = null;
        long? outputTokens = null;
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AnswerPrompt.System),
            new(ChatRole.User, AnswerPrompt.BuildUserMessage(question, context, feedback))
        };
        var chatOptions = new ChatOptions
        {
            ModelId = settings.ChatModel,
            Temperature = settings.Temperature,
            Seed = settings.Seed,
            MaxOutputTokens = settings.MaxOutputTokens
        };

        // Döngünün koşulu yoktur: her tur ya geçerli bir yanıtla döner ya da son denemede istisna fırlatır.
        for (var attempt = 1; ; attempt++)
        {
            ChatResponse<AnswerPayload> response;

            try
            {
                response = await chatClient.GetResponseAsync<AnswerPayload>(
                    messages,
                    SchemaOptions,
                    chatOptions,
                    useJsonSchemaResponseFormat: settings.UseJsonSchema,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Zaman aşımı da (çağıran iptal etmemişken gelen OperationCanceledException) bu dala düşer ve 503 olur.
                // Yalnızca çağıranın kendi iptali sarmalanmadan yukarı taşınır; kapanan bir istek model arızası sayılmaz.
                throw new AnswerGenerationException(AnswerGenerationFailure.Unavailable, $"The language model endpoint failed: {exception.Message}", exception);
            }

            // Şemaya uymak yetmez: listelerde null öğe olmamalı ve yanıt metni olmadan "answerable" ne bir yanıttır ne de
            // bir ret. Eksik zorunlu alanlar ise ayrıştırmada hata verir ve TryGetResult false döner.
            inputTokens = Sum(inputTokens, response.Usage?.InputTokenCount);
            outputTokens = Sum(outputTokens, response.Usage?.OutputTokenCount);

            if (response.TryGetResult(out var payload) && IsComplete(payload) && !(payload.Answerable && string.IsNullOrWhiteSpace(payload.Answer)))
            {
                return ToGeneratedAnswer(payload, settings, attempt, inputTokens, outputTokens);
            }

            if (attempt >= attemptBudget)
            {
                throw new AnswerGenerationException(AnswerGenerationFailure.InvalidOutput, "The language model did not return the required JSON structure.");
            }

            logger.LogWarning(
                "The language model returned output that does not match the answer schema; retrying (attempt {Attempt} of {Budget}).",
                attempt + 1,
                attemptBudget);
            // Model neyi yanlış yaptığını görebilsin diye kendi geçersiz çıktısı konuşmaya eklenir; ardından düzeltici
            // talimat gelir. Çıktıdaki sohbet şablonu belirteçleri silinir: llama.cpp metni özel belirteçleri tanıyarak
            // böldüğü için, modelin ürettiği bir "<|turn>" ikinci istekte gerçek bir sıra belirtecine dönüşürdü.
            messages.Add(new ChatMessage(ChatRole.Assistant, PromptInjectionDetector.RemoveChatTemplateTokens(response.Text)));
            messages.Add(new ChatMessage(ChatRole.User, AnswerPrompt.RetryInstruction));
        }
    }

    /// <summary>
    /// Ayrıştırılmış yanıtın kullanılabilir olup olmadığını denetler: nesne null değildir ve atıf, çelişki ve elenen
    /// kimlik listelerinde <c>null</c> öğe yoktur.
    /// </summary>
    /// <remarks>
    /// Null olamaz işaretleri (<c>RespectNullableAnnotations</c>) özelliklerin kendisine uygulanır, liste öğelerine
    /// uygulanmaz: <c>"citations":[null]</c> ayrıştırmadan geçer ve ilk kullanıldığı yerde <c>NullReferenceException</c>
    /// ile 500 hatasına dönüşürdü. Böyle bir çıktı da şemaya uymayan çıktı gibi düzeltme denemesine yönlendirilir; yine
    /// olmazsa 502 olur.
    /// </remarks>
    private static bool IsComplete([NotNullWhen(true)] AnswerPayload? payload) =>
        payload is not null
        && payload.Citations.All(citation => citation is not null)
        && payload.Conflicts.All(conflict => conflict is not null && conflict.RejectedChunkIds.All(label => label is not null));

    /// <summary>
    /// Model yanıtını (<see cref="AnswerPayload"/>) Application katmanının sağlayıcıdan bağımsız
    /// <see cref="GeneratedAnswer"/> kaydına çevirir; Application böylece şema DTO'sunu ve SDK türlerini tanımaz.
    /// </summary>
    /// <remarks>
    /// <c>??</c> korumaları savunma amaçlıdır: özellikler null olamaz tanımlanmıştır ama dışarıdan gelen veriye tam
    /// güvenilmez. Kimliği boş atıflar burada atılır; etiketin verilen kaynaklardan birine eşlenmesi ve alıntının
    /// doğrulanması Application tarafındaki <c>CitationValidator</c>'ın işidir. Model adı olarak sunucunun döndürdüğü
    /// kimlik değil, yapılandırılan ad raporlanır. Çağrı sayısı ve token kullanımı, şema yeniden denemesi dahil bütün
    /// denemelerin toplamıdır. Serbest metin alanları (yanıt, eksik bilgi, çelişki konusu ve gerekçesi)
    /// <see cref="SystemPromptLeakDetector"/> ile taranır ve sistem prompt'unu tekrarlayan yanıt
    /// <see cref="GeneratedAnswer.LeaksSystemPrompt"/> ile işaretlenir; reddetme kararı handler'ındır.
    /// </remarks>
    /// <param name="payload">Ayrıştırılmış ve eksiksiz olduğu denetlenmiş model yanıtı.</param>
    /// <param name="settings">Raporlanacak model adının alındığı yapılandırma.</param>
    /// <param name="attempts">Bu yanıt için sunucuya giden istek sayısı (şema yeniden denemesi dahil).</param>
    /// <param name="inputTokens">Bütün denemelerin toplam girdi token sayısı; sağlayıcı bildirmediyse null.</param>
    /// <param name="outputTokens">Bütün denemelerin toplam çıktı token sayısı; sağlayıcı bildirmediyse null.</param>
    private static GeneratedAnswer ToGeneratedAnswer(AnswerPayload payload, LlmOptions settings, int attempts, long? inputTokens, long? outputTokens)
    {
        var answer = payload.Answer ?? string.Empty;
        var missingInformation = payload.MissingInformation ?? string.Empty;
        var conflicts = (payload.Conflicts ?? [])
            .Select(conflict => new GeneratedConflict(
                conflict.Topic ?? string.Empty,
                conflict.ChosenChunkId ?? string.Empty,
                conflict.RejectedChunkIds ?? [],
                conflict.Reason ?? string.Empty))
            .ToList();

        // Çıktı koruması: istemciye dönebilecek serbest metin alanlarından biri sistem prompt'unu tekrarlıyorsa yanıt
        // işaretlenir. Alıntılar taranmaz; onlar ancak kaynak metninde birebir doğrulanırsa gösterilir.
        var leaksSystemPrompt = SystemPromptLeakDetector.Repeats(answer)
            || SystemPromptLeakDetector.Repeats(missingInformation)
            || conflicts.Any(conflict => SystemPromptLeakDetector.Repeats(conflict.Topic) || SystemPromptLeakDetector.Repeats(conflict.Reason));

        return new GeneratedAnswer(
            payload.Answerable,
            answer,
            (payload.Citations ?? [])
                .Where(citation => !string.IsNullOrWhiteSpace(citation.ChunkId))
                .Select(citation => new GeneratedCitation(citation.ChunkId, citation.Quote ?? string.Empty))
                .ToList(),
            missingInformation,
            conflicts,
            // response.ModelId değil, yapılandırılan ad: llama.cpp orada yerel model dosyasının yolunu döndürür ve bu
            // yol yanıtın diagnostics bölümüne ve denetim kaydına makine ayrıntısı sızdırırdı.
            settings.ChatModel,
            inputTokens,
            outputTokens,
            attempts,
            leaksSystemPrompt);
    }

    /// <summary>Bilinen token sayılarını toplar; yeni değer bilinmiyorsa (null) mevcut toplamı korur.</summary>
    /// <remarks>Sağlayıcı kullanım bilgisi döndürmezse toplam null kalır; bilinmeyen bir değer 0 diye gösterilmez.</remarks>
    private static long? Sum(long? total, long? value) => value is null ? total : (total ?? 0) + value;
}
