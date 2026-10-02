using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;

namespace Knowledge.Infrastructure.Llm;

/// <summary>
/// <c>Llm:BaseUrl</c> boş olduğunda kullanılan yer tutucu üreteç. Uygulama dil modeli olmadan da açılır: arama,
/// doküman ve sağlık uçları çalışır (sağlık durumu <c>degraded</c> görünür); modele ulaşması gereken sorular ise
/// modelin kullanılamadığını bildiren net bir 503 (<c>LlmUnavailable</c>) alır.
/// </summary>
/// <remarks>
/// Açılışta hata vermek yerine bu sınıfın seçilmesi, model sunucusu olmayan veya henüz hazır olmayan ortamlarda da
/// uygulamanın kullanılabilir kalmasını sağlar (ör. yalnızca arama kalitesini ölçmek için). Kapı 1'de veya sürüm
/// çözümünde reddedilen sorular modeli hiç çağırmadığı için bu durumda da normal "bilgi yok" yanıtını alır.
/// </remarks>
public sealed class UnconfiguredAnswerGenerator : IGroundedAnswerGenerator
{
    /// <summary>
    /// Her zaman <c>false</c>; sağlık uç noktası bu değerle dil modelini yapılandırılmamış ve sistemi
    /// <c>degraded</c> olarak raporlar.
    /// </summary>
    public bool IsConfigured => false;

    /// <summary>Boş ad: yapılandırılmış bir dil modeli yoktur.</summary>
    public string ModelName => string.Empty;

    /// <summary>
    /// Her zaman <see cref="AnswerGenerationFailure.Unavailable"/> nedeniyle <see cref="AnswerGenerationException"/>
    /// fırlatır. Komut işleyicisi bunu gerçek bir erişim hatasıyla aynı yoldan 503'e çevirir; böylece "model yok"
    /// durumu için ayrı bir kod yolu gerekmez. Geri bildirim parametresi yalnızca port sözleşmesi gereği vardır; ilk
    /// çağrı zaten hata verdiği için düzeltme turu hiç oluşmaz.
    /// </summary>
    public Task<GeneratedAnswer> GenerateAsync(
        string question,
        IReadOnlyList<ContextChunk> context,
        AnswerFeedback? feedback = null,
        CancellationToken cancellationToken = default)
        => throw new AnswerGenerationException(AnswerGenerationFailure.Unavailable, "No language model is configured (Llm:BaseUrl is empty).");
}
