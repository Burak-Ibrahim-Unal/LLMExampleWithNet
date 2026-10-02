namespace Knowledge.Domain.Entities;

/// <summary>
/// Dokümanın türü; aynı zamanda kaynaklar çeliştiğinde kullanılan yetki sırasıdır (politika/prosedür &gt; kılavuz &gt;
/// SSS). Front matter'daki <c>category</c> alanından (<c>politika</c> | <c>prosedur</c> | <c>kilavuz</c> | <c>sss</c>)
/// okunur ve API'de aynı sözcüklerle gösterilir.
/// </summary>
/// <remarks>
/// Yetki sırasını <c>SourcePrecedence</c> uygular: farklı türlerde yetkisi yüksek olan, aynı yetkide yürürlük tarihi daha
/// yeni olan kazanır. Gerekçe: politika, sahibinin onayladığı bağlayıcı metindir; SSS ise ondan geride kalabilen bir
/// özettir. Bu sıra yalnızca farklı dokümanlar çeliştiğinde kullanılır; aynı doküman ailesinin sürümleri arasındaki seçim
/// ayrı ve deterministik olarak <c>VersionResolver</c>'da yapılır.
/// </remarks>
public enum DocumentCategory
{
    /// <summary>Politika (<c>politika</c>): bağlayıcı şirket kuralları, ör. iade politikası, garanti koşulları. En yüksek yetki.</summary>
    Policy,
    /// <summary>Prosedür (<c>prosedur</c>): adım adım işleyiş, ör. şikâyet eskalasyonu. Politikayla aynı yetki düzeyindedir.</summary>
    Procedure,
    /// <summary>Kılavuz (<c>kilavuz</c>): kurulum ve sorun giderme rehberleri; politika ve prosedürün ardından gelir.</summary>
    Guide,
    /// <summary>Sıkça sorulan sorular (<c>sss</c>): diğer dokümanların özeti niteliğinde olduğu için en düşük yetki.</summary>
    Faq
}
