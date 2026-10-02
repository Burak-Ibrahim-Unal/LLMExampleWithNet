using System.Reflection;

namespace Knowledge.Application;

/// <summary>
/// Knowledge.Application derlemesine tip güvenli bir başvuru noktası sağlar. Derlemeyi tarayan kayıtlar (MediatR'ın
/// komut/sorgu handler'larını bulması) ve NetArchTest mimari testleri derlemeyi adıyla aramak yerine bu sınıf
/// üzerinden alır.
/// </summary>
/// <remarks>
/// Derleme adı değişirse bu başvuru derleme zamanında kırılır; dize tabanlı bir <c>Assembly.Load("...")</c> ise
/// ancak çalışma anında hata verirdi. Rastgele bir handler tipine <c>typeof</c> ile bağlanmaktan da daha
/// sağlamdır: o tip taşındığında veya silindiğinde başvurunun anlamı kaybolmaz.
/// </remarks>
public static class AssemblyReference
{
    /// <summary>Knowledge.Application derlemesinin kendisi; DI taramasında ve mimari testlerde kullanılır.</summary>
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
