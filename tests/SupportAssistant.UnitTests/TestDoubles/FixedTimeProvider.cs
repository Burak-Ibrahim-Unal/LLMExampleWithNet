namespace SupportAssistant.UnitTests.TestDoubles;

/// <summary>
/// "Bugün"ü sabit bir tarihe kilitleyen <see cref="TimeProvider"/>. <c>VersionResolver</c> hangi sürümün yürürlükte
/// olduğuna karar verirken bugünün tarihini enjekte edilen <c>TimeProvider</c>'dan okur; testler bu sınıfla kararı gerçek
/// takvimden bağımsız ve tekrarlanabilir kılar.
/// </summary>
/// <remarks>
/// Sabit saat olmasaydı 2027-01-01'de yürürlüğe girecek bir sürümün "henüz yürürlükte değil" sayıldığını doğrulayan
/// testler, o tarih geldiğinde kendiliğinden kırılırdı.
/// </remarks>
/// <param name="today">Testin "bugün" kabul ettiği takvim günü.</param>
internal sealed class FixedTimeProvider(DateOnly today) : TimeProvider
{
    /// <summary>
    /// Verilen günün 09:00 UTC anını döndürür. <c>VersionResolver</c> <c>GetLocalNow()</c> kullandığından bu an makinenin
    /// saat dilimine çevrilir; 09:00 UTC, UTC−9 ile UTC+14 arasındaki her saat diliminde aynı takvim gününe düşer. Böylece
    /// "bugün" pratikte testin çalıştığı makineden bağımsız kalır.
    /// </summary>
    public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
}
