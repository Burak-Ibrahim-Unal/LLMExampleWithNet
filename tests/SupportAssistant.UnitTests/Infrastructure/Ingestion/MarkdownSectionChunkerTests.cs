using Knowledge.Application.Abstractions;
using Knowledge.Infrastructure.Ingestion;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Ingestion;

/// <summary>
/// <see cref="MarkdownSectionChunker"/> birim testleri. Chunk sınırları atıf kalitesini doğrudan belirler: her yanıt
/// kullandığı dokümanı ve ilgili bölümü göstermek zorundadır ve gösterilen bölüm yolu burada üretilir. Testler saf metin
/// girdileriyle çalışır; dosya sistemi ya da front matter gerekmez.
/// </summary>
public sealed class MarkdownSectionChunkerTests
{
    /// <summary>
    /// Her başlık için bir chunk üretildiğini, alt başlıkların üst başlık yoluyla adlandırıldığını
    /// (<c>"2. Seviyeler &gt; 2.1 Birinci"</c>), seviye 1 başlığın (doküman başlığı) yola eklenmediğini ve yalnızca alt
    /// bölümleri gruplayan, kendi metni olmayan <c>## 2. Seviyeler</c> başlığının boş chunk üretmediğini doğrular.
    /// </summary>
    /// <remarks>
    /// Tam bölüm yolu, "2.1 Birinci" gibi alt başlıkların hangi üst bölüme ait olduğunu atıfta açıkça gösterir; başlık
    /// sözcükleri BM25 metnine de katıldığı için aramaya bağlam sağlar. Boş chunk'lar ise yalnızca başlık sözcükleriyle
    /// eşleşip bağlamı içeriksiz bölümlerle doldururdu.
    /// </remarks>
    [Fact]
    public void Split_creates_one_chunk_per_heading_with_its_parent_path_and_skips_empty_sections()
    {
        const string body = "# Başlık\n\n## 1. Giriş\n\nMerhaba dünya.\n\n## 2. Seviyeler\n\n### 2.1 Birinci\n\nBirinci metin.\n\n### 2.2 İkinci\n\nİkinci metin.\n";

        var sections = MarkdownSectionChunker.Split(body, "Başlık");

        sections.ShouldBe(
        [
            new SourceSection("1. Giriş", "Merhaba dünya."),
            new SourceSection("2. Seviyeler > 2.1 Birinci", "Birinci metin."),
            new SourceSection("2. Seviyeler > 2.2 İkinci", "İkinci metin.")
        ]);
    }

    /// <summary>
    /// İlk <c>##</c> başlığından önceki giriş metninin kaybolmadığını ve bölüm yolu olarak doküman başlığını aldığını
    /// doğrular. Giriş paragrafları çoğu zaman kapsam veya tanım içerir; atılsaydı bu bilgiler aranamaz, boş bir yolla
    /// saklansaydı atıfta anlamsız bir bölüm adı görünürdü.
    /// </summary>
    [Fact]
    public void Split_uses_the_document_title_as_path_for_text_before_the_first_section()
    {
        var sections = MarkdownSectionChunker.Split("# Başlık\n\nGiriş paragrafı.\n\n## A\n\nMetin.", "Başlık");

        sections.ShouldBe([new SourceSection("Başlık", "Giriş paragrafı."), new SourceSection("A", "Metin.")]);
    }

    /// <summary>
    /// Sınırı (<c>maxChunkChars</c>, burada 55) aşan bir bölümün paragraf sınırlarından bölündüğünü doğrular: ilk iki
    /// paragraf (49 karakter) birlikte sığar, üçüncüsü yeni bir chunk'a geçer ve her parça aynı bölüm yolunu korur.
    /// </summary>
    /// <remarks>
    /// Varsayılan sınır 1200 karakterdir; çok uzun bir bölüm tek chunk olsaydı embedding'i odağını kaybeder ve modele
    /// gereksiz uzun bağlam giderdi. Paragraf ortasından kesmek ise bir kuralı koşulundan ayırabilir. Parçaların aynı yolu
    /// taşıması, hangi parça kullanılırsa kullanılsın atfın doğru bölümü göstermesini sağlar.
    /// </remarks>
    [Fact]
    public void Split_breaks_long_sections_at_paragraph_boundaries()
    {
        const string body = "## Uzun\n\nBirinci paragraf burada.\n\nİkinci paragraf burada.\n\nÜçüncü paragraf burada.";

        var sections = MarkdownSectionChunker.Split(body, "Belge", maxChunkChars: 55);

        sections.ShouldBe(
        [
            new SourceSection("Uzun", "Birinci paragraf burada.\n\nİkinci paragraf burada."),
            new SourceSection("Uzun", "Üçüncü paragraf burada.")
        ]);
    }
}
