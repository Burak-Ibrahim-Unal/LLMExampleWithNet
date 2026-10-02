namespace Knowledge.Application.Exceptions;

/// <summary>
/// Yanıt üretiminin teknik olarak neden başarısız olduğu. Soru handler'ı bunu farklı HTTP durumlarına çevirir:
/// <c>Unavailable</c> → 503 (<c>LlmUnavailable</c>), <c>InvalidOutput</c> → 502 (<c>LlmInvalidOutput</c>).
/// </summary>
/// <remarks>
/// İki durum bilinçli olarak ayrılır ve hiçbiri "bilgi yok" diye geçiştirilmez: model sunucusuna ulaşılamaması geçici bir
/// hizmet sorunudur (503, daha sonra tekrar denenebilir); geçersiz çıktı ise arkadaki sunucunun bozuk yanıtıdır (502).
/// </remarks>
public enum AnswerGenerationFailure
{
    /// <summary>
    /// Model ucuna ulaşılamadı, zaman aşımı oldu ya da sunucu hata döndürdü. Hiç model yapılandırılmamışsa da bu değer kullanılır.
    /// </summary>
    Unavailable,

    /// <summary>Model yanıt verdi ama bir yeniden denemeden sonra bile istenen yapıda değil.</summary>
    InvalidOutput
}

/// <summary>
/// Dil modeli adımının teknik hatası. <c>IGroundedAnswerGenerator</c> adaptörleri SDK hatalarını ve geçersiz çıktıyı bu
/// tipe çevirir; böylece Application katmanı hiçbir LLM SDK'sını tanımadan hatayı sınıflandırabilir.
/// </summary>
/// <remarks>
/// Mesaj teknik ayrıntı içerir ve yalnızca loglanır; istemci <c>Messages.Knowledge</c> içindeki sabit Türkçe metni görür.
/// </remarks>
/// <param name="failure">Hatanın türü; HTTP durum kodunu belirler.</param>
/// <param name="message">Loglar için teknik açıklama.</param>
/// <param name="innerException">Varsa, hatanın asıl kaynağı olan SDK/HTTP istisnası.</param>
public sealed class AnswerGenerationException(AnswerGenerationFailure failure, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Hatanın türü; handler 503 ile 502 arasında buna göre seçim yapar.</summary>
    public AnswerGenerationFailure Failure { get; } = failure;
}
