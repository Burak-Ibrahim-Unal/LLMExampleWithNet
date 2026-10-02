namespace Knowledge.Application.Answering;

/// <summary>Bir yanıtın neden verilmediği; cevaplama hattının her kapısı için bir değer.</summary>
/// <remarks>
/// Değer API yanıtının <c>refusalReason</c> alanında döner, soru loguna yazılır ve değerlendirme raporunda gösterilir.
/// Sabit, makinece okunabilir kodlar seçildi: istemci ve değerlendirme aracı reddin hangi aşamada olduğunu metin
/// ayrıştırmadan ayırt edebilir; kullanıcıya gösterilen Türkçe "yeterli bilgi yok" mesajı bundan ayrıdır ve her rette aynıdır.
/// </remarks>
public static class RefusalReasons
{
    /// <summary>Kapı 1: arama soruya yeterince yakın bir bölüm bulamadı; model çağrılmadı.</summary>
    public const string LowRelevance = "LowRelevance";

    /// <summary>
    /// Arama yalnızca yürürlükte olmayan sürümler buldu (superseded ya da ileri tarihli); model çağrılmadı. Eski bir kural
    /// modele hiç ulaşmasın diye bu durumda yanıt üretilmez.
    /// </summary>
    public const string NoSourceInEffect = "NoSourceInEffect";

    /// <summary>Kapı 2: model verilen kaynakları yetersiz buldu (<c>answerable=false</c>).</summary>
    public const string ModelInsufficientContext = "ModelInsufficientContext";

    /// <summary>
    /// Kapı 3: model yanıt verdi ama verilen kaynaklardan hiçbirine geçerli atıf yapmadı (ya da temizlikten sonra ne
    /// yanıt metni ne de alıntı kaldı).
    /// </summary>
    public const string NoValidCitations = "NoValidCitations";
}
