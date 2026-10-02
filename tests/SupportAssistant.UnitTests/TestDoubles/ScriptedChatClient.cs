using Microsoft.Extensions.AI;

namespace SupportAssistant.UnitTests.TestDoubles;

/// <summary>
/// Model uç noktasının yerine geçen sahte <c>IChatClient</c>: hazır yanıtları sırayla tekrar oynatır ve gelen istekleri
/// kaydeder. n'inci istek n'inci yanıtı alır, yanıtlar tükenirse sonuncusu tekrarlanır. Gerçek llama.cpp sunucusu gibi
/// yanıtta model kimliği olarak bir dosya yolu ve her yanıt için 100 girdi / 20 çıktı token'lık kullanım bilgisi döndürür.
/// </summary>
/// <remarks>
/// Model uç noktası sistemin dış sınırıdır; bu sahte istemci sayesinde gerçek üretici (MEAI'nin yapılandırılmış çıktı
/// katmanı dahil) ağ ve model olmadan çalışır. Hem üreticinin kendi testleri hem de üretici ile handler'ın birlikte
/// çalıştığı akış testleri bunu kullanır; <see cref="Requests"/> sunucuya gerçekte kaç istek gittiğini gösterir.
/// </remarks>
/// <param name="replies">Sırayla döndürülecek ham yanıt metinleri (genellikle JSON).</param>
internal sealed class ScriptedChatClient(params string[] replies) : IChatClient
{
    /// <summary>
    /// Her çağrıda gönderilen mesaj listesi (sistem + kullanıcı; şema yeniden denemesinde ek olarak önceki yanıt ve
    /// düzeltici talimat). Listenin uzunluğu modele kaç kez gidildiğini gösterir.
    /// </summary>
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    /// <summary>Her çağrıda iletilen <c>ChatOptions</c>; örnekleme ayarlarının ve şemanın isteğe ulaştığını doğrulamak için tutulur.</summary>
    public List<ChatOptions?> Options { get; } = [];

    /// <summary>
    /// Ayarlanırsa her çağrı bu istisnayla başarısız olur; bağlantı reddi gibi taşıma katmanı hatalarını taklit eder.
    /// Bu durumda istek kaydedilmez.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// <c>Failure</c> ayarlıysa onunla başarısız olan bir görev döndürür; değilse isteği ve seçenekleri kaydedip sıradaki
    /// hazır yanıtı asistan mesajı olarak verir.
    /// </summary>
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            return Task.FromException<ChatResponse>(Failure);
        }

        Requests.Add(messages.ToList());
        Options.Add(options);
        var reply = replies[Math.Min(Requests.Count - 1, replies.Length - 1)];

        // llama.cpp, model kimliği olarak model dosyasının yolunu döndürür.
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply))
        {
            ModelId = "/home/someone/models/gemma.gguf",
            Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 20 }
        });
    }

    /// <summary>
    /// Üretici akışlı (streaming) çağrı kullanmaz; böyle bir çağrıya geçilirse test görünür biçimde başarısız olsun diye
    /// <c>NotSupportedException</c> fırlatır.
    /// </summary>
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <summary>Ek servis sunmaz; yalnızca <c>IChatClient</c> sözleşmesi gereği vardır.</summary>
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <summary>Serbest bırakılacak kaynak yoktur; arayüz sözleşmesi gereği boştur.</summary>
    public void Dispose()
    {
    }
}
