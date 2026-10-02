using Knowledge.Domain.Entities;

namespace Knowledge.Application.Abstractions;

/// <summary>
/// Bilgi tabanı dokümanlarını (doğruluğun tek kaynağı) okuyup bölümlere ayıran port.
/// </summary>
/// <remarks>
/// Veritabanı yalnızca bu kaynaktan türetilmiş veridir; her ingest buradan okur ve veritabanını dosyalarla uzlaştırır.
/// Port sayesinde ingest handler'ı dosya sistemini, YAML ve Markdown ayrıştırmayı bilmez: üretimde
/// <c>MarkdownKnowledgeSource</c> <c>knowledge-base/*.md</c> dosyalarını okur, birim testlerinde
/// <c>StubKnowledgeBaseSource</c> bellekteki dokümanları döndürür, entegrasyon testleri ise hata fırlatan bir kaynakla
/// açılışın bu hataya dayanıklı olduğunu sınar.
/// </remarks>
public interface IKnowledgeBaseSource
{
    /// <summary>Tüm dokümanları meta verileri ve bölümleriyle birlikte okur.</summary>
    /// <remarks>
    /// Biçim hataları <c>KnowledgeBaseFormatException</c> olarak fırlatılmalıdır: ingest bunu dosya adını içeren mesajla
    /// 422'ye çevirir. <c>IOException</c> ve <c>UnauthorizedAccessException</c> ise ayrıntısı yalnızca loglanan genel bir
    /// 422 mesajına dönüşür.
    /// </remarks>
    Task<IReadOnlyList<SourceDocument>> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Kaynaktan okunmuş tek bir doküman sürümü: front matter meta verisi ve bölümlere ayrılmış içerik.</summary>
/// <param name="SourceId">Front matter'daki <c>id</c>; sürüme özgü kimlik (ör. "iade-politikasi-v2"). Ingest veritabanındaki kayıtla bu kimlikle eşleştirir.</param>
/// <param name="DocumentKey">Doküman ailesi; aynı prosedürün tüm sürümleri bu anahtarı paylaşır ve sürüm çözümü buna göre gruplar.</param>
/// <param name="Title">Doküman başlığı; atıflarda gösterilir ve embedding metninin başına eklenir.</param>
/// <param name="Version">Sürüm numarası (ör. "2.0"); aynı yürürlük tarihine sahip sürümleri sıralamakta kullanılır.</param>
/// <param name="EffectiveDate">Yürürlük tarihi; bu tarih gelmeden doküman yanıtlarda kullanılmaz.</param>
/// <param name="Status">active veya superseded; superseded işaretli sürüm hiçbir zaman seçilmez.</param>
/// <param name="Category">Doküman türü (politika, prosedür, kılavuz, SSS); farklı dokümanlar çeliştiğinde öncelik sırasını belirler.</param>
/// <param name="Supersedes">Bu sürümün yerini aldığı sürümün kimliği; isteğe bağlı ve bilgi amaçlıdır, sürüm seçimi tarih ve status alanlarına dayanır.</param>
/// <param name="ContentHash">
/// Dosya içeriğinin özeti (Markdown kaynağında satır sonları normalleştirilmiş metnin SHA-256'sı). Özet değişmediyse ingest
/// dokümanı yeniden bölmez ve uzak embedding sunucusunu tekrar çağırmaz.
/// </param>
/// <param name="Sections">Her <c>##</c>/<c>###</c> başlığı için bir bölüm; uzun bölümler paragraf sınırlarından bölünür.</param>
public sealed record SourceDocument(
    string SourceId,
    string DocumentKey,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    DocumentStatus Status,
    DocumentCategory Category,
    string? Supersedes,
    string ContentHash,
    IReadOnlyList<SourceSection> Sections);

/// <summary>Dokümanın tek bir bölümü; arama, bağlam ve atıf bu birim üzerinden yapılır.</summary>
/// <param name="SectionPath">
/// Başlık yolu (ör. "2. Destek Seviyeleri &gt; 2.2 Seviye 2 (L2)"); yanıtta hangi bölümün kullanıldığını göstermek için
/// saklanır. Başlığı olmayan içerik için doküman başlığıdır.
/// </param>
/// <param name="Content">Bölümün metni (başlık satırı hariç).</param>
public sealed record SourceSection(string SectionPath, string Content);
