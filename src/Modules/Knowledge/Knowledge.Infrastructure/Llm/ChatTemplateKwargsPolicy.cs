using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Knowledge.Infrastructure.Llm;

/// <summary>
/// Giden sohbet isteklerinin JSON gövdesine <c>"chat_template_kwargs": {"enable_thinking": …}</c> alanını ekleyen
/// System.ClientModel pipeline politikası. llama.cpp sunucusu (ve vLLM), Gemma'nın düşünme modu gibi chat şablonu
/// anahtarlarını bu standart dışı alandan okur. OpenAI SDK'sında bu alan için tipli bir seçenek yoktur ve istek
/// gövdesini SDK kendisi serileştirir; bu yüzden alan, gövde HTTP hattından (pipeline) geçerken eklenir.
/// </summary>
/// <remarks>
/// Politika yalnızca <see cref="LlmOptions.EnableThinking"/> bir değer taşıdığında istemciye eklenir; böylece alanı
/// tanımayan sağlayıcılar (OpenAI, Gemini) onu hiç görmez. Alanı HTTP katmanında eklemek, cevap üretecini ve
/// Microsoft.Extensions.AI kodunu sağlayıcıya özgü bu ayrıntıdan arındırır.
/// </remarks>
public sealed class ChatTemplateKwargsPolicy(bool enableThinking) : PipelinePolicy
{
    /// <summary>
    /// Senkron hat yolu: gövdeyi değiştirir ve isteği hattaki bir sonraki politikaya devreder.
    /// <see cref="PipelinePolicy"/> senkron ve asenkron yolları ayrı ayrı soyut tanımladığından ikisi de uygulanmalıdır;
    /// SDK senkron bir metotla çağrıldığında bu yol çalışır. <c>ProcessNext</c> çağrılmasaydı istek hiç gönderilmezdi.
    /// </summary>
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Apply(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    /// <summary>
    /// Asenkron hat yolu (Microsoft.Extensions.AI istemcisi bunu kullanır): senkron yolla aynı değişikliği yapar ve
    /// isteği bir sonraki politikaya devreder.
    /// </summary>
    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Apply(message);
        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
    }

    /// <summary>
    /// İstek gövdesine <c>chat_template_kwargs.enable_thinking</c> değerini yazar ve yeni gövdeyi UTF-8 bayt olarak
    /// döndürür. Gövdede zaten bir <c>chat_template_kwargs</c> nesnesi varsa içindeki diğer anahtarlar korunur; yoksa
    /// (veya bu alan bir nesne değilse) yeni bir nesne oluşturulur.
    /// </summary>
    /// <remarks>
    /// Saf bir dönüşüm olduğu için <c>public static</c> tutulur ve pipeline kurmadan doğrudan birim testiyle doğrulanır.
    /// Gövde ayrıştırılamıyorsa veya bir JSON nesnesi değilse bozuk bir istek göndermek yerine istisna fırlatılır; bu
    /// hata cevap üretecinde "modele ulaşılamıyor" (503) olarak raporlanır.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Gövde geçerli JSON olup bir nesne değilse (ör. bir dizi).</exception>
    public static byte[] AddEnableThinking(byte[] requestBody, bool enableThinking)
    {
        var body = JsonNode.Parse(requestBody) as JsonObject
            ?? throw new InvalidOperationException("The chat completion request body is not a JSON object.");

        if (body["chat_template_kwargs"] is not JsonObject templateArguments)
        {
            templateArguments = new JsonObject();
            body["chat_template_kwargs"] = templateArguments;
        }

        templateArguments["enable_thinking"] = enableThinking;
        return JsonSerializer.SerializeToUtf8Bytes(body);
    }

    /// <summary>
    /// Dönüşümü yalnızca gövdesi olan <c>/chat/completions</c> isteklerine uygular; diğer çağrılar (ör. gövdesiz GET
    /// istekleri) olduğu gibi geçer. SDK'nın <c>BinaryContent</c>'i okunamaz, yalnızca bir akışa yazılabilir; bu yüzden
    /// gövde önce belleğe kopyalanır, değiştirilir ve yeni içerik olarak isteğe konur.
    /// </summary>
    /// <remarks>
    /// Politika <c>PipelinePosition.PerCall</c> konumunda eklendiği için bu dönüşüm mantıksal çağrı başına bir kez,
    /// istemcinin yeniden deneme politikasından önce yapılır; yeniden denemeler zaten değiştirilmiş gövdeyi gönderir.
    /// </remarks>
    private void Apply(PipelineMessage message)
    {
        var request = message.Request;

        if (request.Content is null || request.Uri?.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal) != true)
        {
            return;
        }

        using var buffer = new MemoryStream();
        request.Content.WriteTo(buffer);
        request.Content = BinaryContent.Create(BinaryData.FromBytes(AddEnableThinking(buffer.ToArray(), enableThinking)));
    }
}
