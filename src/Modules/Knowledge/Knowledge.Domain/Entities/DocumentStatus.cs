namespace Knowledge.Domain.Entities;

/// <summary>
/// Bir doküman sürümünün yaşam döngüsü durumu; front matter'daki <c>status</c> alanından (<c>active</c> |
/// <c>superseded</c>) okunur ve API'de aynı sözcüklerle gösterilir.
/// </summary>
/// <remarks>
/// Durum, yürürlük tarihinden bağımsız açık bir işarettir: <c>VersionResolver</c> bir aileden sürüm seçerken
/// <see cref="Superseded"/> işaretli sürümü tarihine bakmaksızın hiçbir zaman seçmez. Bu, tek sürümlü bir dokümanı da
/// kullanımdan kaldırmaya imkân verir.
/// </remarks>
public enum DocumentStatus
{
    /// <summary>Geçerli sürüm; yürürlük tarihi geldiyse yanıtlarda kaynak olabilir.</summary>
    Active,
    /// <summary>
    /// Yerine yenisi gelmiş sürüm. Saklanır, listelenir ve aramada görünür (sürüm geçmişi izlenebilsin diye), ama dil
    /// modeline hiçbir zaman kaynak olarak verilmez; aramada eşleşip yanıtın dayandığı aileye aitse, yanıttaki
    /// <c>versionResolution.discarded</c> listesinde elenme gerekçesiyle raporlanır.
    /// </summary>
    Superseded
}
