using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Ingestion;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Ingestion;

/// <summary>
/// <see cref="MarkdownDocumentParser"/> birim testleri: YAML front matter ve Markdown gövdeden oluşan tek bir bilgi
/// tabanı dosyasının <c>SourceDocument</c>'a dönüştürülmesini, hatalı dosyaların anlaşılır bir
/// <c>KnowledgeBaseFormatException</c> ile reddedilmesini ve içerik hash'inin kararlılığını dosya sistemi olmadan,
/// bellekteki metinlerle doğrular. Front matter'daki <c>documentKey</c>, <c>effectiveDate</c> ve <c>status</c> sürüm
/// çözümlemesinin girdisidir; buradaki bir ayrıştırma hatası yanıtta yanlış sürümün kullanılmasına yol açar.
/// </summary>
public sealed class MarkdownDocumentParserTests
{
    /// <summary>Hata mesajlarında görünmesi beklenen dosya adı; operatör hatalı dosyayı mesajdan bulabilmelidir.</summary>
    private const string FileName = "iade-v2.md";

    /// <summary>
    /// Tüm zorunlu alanları içeren geçerli bir doküman. Windows'ta Git bilgi tabanını CRLF satır sonlarıyla checkout
    /// eder; örnek bu yüzden bilerek CRLF kullanır. Hata testleri bu metnin tek bir satırını değiştirerek bozuk
    /// varyantlar üretir.
    /// </summary>
    private const string Valid =
        "---\r\nid: iade-v2\r\ndocumentKey: iade\r\ntitle: İade Politikası\r\nversion: \"2.0\"\r\neffectiveDate: 2025-06-01\r\n" +
        "status: active\r\nsupersedes: iade-v1\r\ncategory: politika\r\n---\r\n\r\n# İade Politikası\r\n\r\n" +
        "## 2. İade Süresi\r\n\r\n30 gün içinde iade edebilirsiniz.\r\n";

    /// <summary>
    /// CRLF satır sonlu bir dosyada front matter alanlarının (<c>id</c> → <c>SourceId</c>, <c>documentKey</c>,
    /// <c>title</c>, <c>version</c>, <c>effectiveDate</c>, <c>status</c>, <c>category</c>, <c>supersedes</c>) doğru
    /// tiplere eşlendiğini ve gövdenin bölümlere ayrıldığını doğrular; seviye 1 başlık bölüm sayılmaz, bölüm içeriğinde
    /// <c>\r</c> kalmaz.
    /// </summary>
    /// <remarks>
    /// Satır sonları normalize edilmeseydi <c>---\n</c> ayırıcısı CRLF dosyalarda bulunamaz ve Windows checkout'unda bilgi
    /// tabanının tamamı reddedilirdi. <c>politika</c> → <c>Policy</c> gibi eşlemeler kaynaklar arası öncelik kuralında,
    /// <c>effectiveDate</c> ve <c>status</c> ise yürürlükteki sürümün seçiminde doğrudan kullanılır.
    /// </remarks>
    [Fact]
    public void Parse_reads_front_matter_and_sections_from_crlf_text()
    {
        var document = MarkdownDocumentParser.Parse(FileName, Valid);

        document.SourceId.ShouldBe("iade-v2");
        document.DocumentKey.ShouldBe("iade");
        document.Title.ShouldBe("İade Politikası");
        document.Version.ShouldBe("2.0");
        document.EffectiveDate.ShouldBe(new DateOnly(2025, 6, 1));
        document.Status.ShouldBe(DocumentStatus.Active);
        document.Category.ShouldBe(DocumentCategory.Policy);
        document.Supersedes.ShouldBe("iade-v1");
        document.Sections.ShouldBe([new SourceSection("2. İade Süresi", "30 gün içinde iade edebilirsiniz.")]);
    }

    /// <summary>
    /// <c>supersedes: ""</c> değerinin "önceki sürüm yok" (<c>null</c>) olarak okunduğunu doğrular. Bir ailenin ilk sürümü
    /// hiçbir dokümanın yerini almaz; boş metin kimlik gibi saklansaydı doküman API'lerinde (<c>GET /v1/documents</c>)
    /// <c>null</c> yerine boş bir kimlik görünür ve var olmayan bir dokümana referans gibi okunurdu.
    /// </summary>
    [Fact]
    public void Parse_treats_an_empty_supersedes_value_as_none()
    {
        var document = MarkdownDocumentParser.Parse(FileName, Valid.Replace("supersedes: iade-v1", "supersedes: \"\""));

        document.Supersedes.ShouldBeNull();
    }

    /// <summary>
    /// Zorunlu bir alan (burada <c>documentKey</c>) eksik olduğunda dosya adını ve alan adını içeren bir
    /// <c>KnowledgeBaseFormatException</c> fırlatıldığını doğrular. <c>documentKey</c> olmadan doküman hiçbir sürüm
    /// ailesine bağlanamaz; varsayılan bir değer uydurmak yerine hızlı başarısız olunur. Mesaj yeniden indeksleme
    /// yanıtında (422) operatöre gösterildiği için hangi dosyada hangi alanın eksik olduğunu söylemelidir.
    /// </summary>
    [Fact]
    public void Parse_rejects_a_document_without_a_required_field()
    {
        var exception = Should.Throw<KnowledgeBaseFormatException>(
            () => MarkdownDocumentParser.Parse(FileName, Valid.Replace("documentKey: iade\r\n", string.Empty)));

        exception.Message.ShouldContain(FileName);
        exception.Message.ShouldContain("documentKey");
    }

    /// <summary>
    /// Kapalı sözlük dışındaki <c>status</c> (<c>draft</c>) ve <c>category</c> (<c>blog</c>) değerlerinin ve
    /// <c>yyyy-MM-dd</c> dışındaki tarih biçiminin (<c>01.06.2025</c>) reddedildiğini doğrular.
    /// </summary>
    /// <remarks>
    /// <c>status</c> ve <c>effectiveDate</c> hangi sürümün yürürlükte olduğunu belirler; <c>category</c> ise kaynaklar
    /// arası öncelik kuralını (politika/prosedür &gt; kılavuz &gt; SSS) besler. Bilinmeyen bir değeri tahminle bir enum'a
    /// eşlemek ya da gün/ay sırası kültüre göre değişen bir tarihi yorumlamak yanlış sürümün "geçerli" seçilmesine yol
    /// açabilir; bu yüzden ayrıştırıcı katıdır ve hatayı ingest sırasında yüzeye çıkarır.
    /// </remarks>
    [Theory]
    [InlineData("status: active", "status: draft")]
    [InlineData("category: politika", "category: blog")]
    [InlineData("effectiveDate: 2025-06-01", "effectiveDate: 01.06.2025")]
    public void Parse_rejects_unknown_status_category_or_date_format(string original, string replacement)
    {
        Should.Throw<KnowledgeBaseFormatException>(() => MarkdownDocumentParser.Parse(FileName, Valid.Replace(original, replacement)));
    }

    /// <summary>
    /// Boş front matter'ın (açılış <c>---</c> satırının hemen ardından kapanış <c>---</c> satırı) çökme yerine dosya adını
    /// içeren bir <c>KnowledgeBaseFormatException</c> ile reddedildiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Son incelemede bulunup düzeltilen bir hatanın regresyon testidir: iki ayırıcı bitişik olduğunda YAML bölümünü kesen
    /// aralık ters dönüyor ve ayrıştırıcı <c>ArgumentOutOfRangeException</c> ile çöküyordu. Yakalanmayan bu istisna
    /// yeniden indekslemeyi beklenmedik bir sunucu hatasına çevirirken biçim hatası 422 ile hangi dosyanın bozuk olduğunu
    /// söyler.
    /// </remarks>
    [Fact]
    public void Parse_rejects_empty_front_matter_instead_of_crashing()
    {
        var exception = Should.Throw<KnowledgeBaseFormatException>(() => MarkdownDocumentParser.Parse("bos.md", "---\n---\n\n## A\n\nMetin."));

        exception.Message.ShouldContain("bos.md");
    }

    /// <summary>
    /// <c>---</c> ile başlamayan, yani metadata taşımayan bir Markdown dosyasının reddedildiğini doğrular. Sürümü,
    /// yürürlük tarihi ve durumu bilinmeyen bir doküman sürüm çözümlemesine katılamaz; böyle bir dosyayı sessizce
    /// indekslemek, hangi kuralın geçerli olduğu bilinmeyen bir kaynağı modele açmak olurdu.
    /// </summary>
    [Fact]
    public void Parse_rejects_text_without_front_matter()
    {
        Should.Throw<KnowledgeBaseFormatException>(() => MarkdownDocumentParser.Parse(FileName, "# Başlık\n\n## A\n\nMetin."));
    }

    /// <summary>
    /// İçerik hash'inin yalnızca satır sonları farklı (CRLF / LF) iki metin için aynı kaldığını, gerçek bir metin
    /// değişikliğinde ("30 gün" → "45 gün") ise değiştiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Ingest, dokümanları içerik hash'i ve embedding modeli adına göre veritabanıyla uzlaştırır; değişmeyen doküman
    /// yeniden embed edilmez. Hash satır sonlarına duyarlı olsaydı aynı depo Windows'ta ve Linux'ta farklı hash üretir,
    /// her ortam değişiminde tüm dokümanlar uzak ve yavaş embedding sunucusunda yeniden işlenirdi. Tersine, metin
    /// değişikliği hash'e yansımasaydı eski chunk'lar ve vektörler güncel metnin yerine kullanılmaya devam ederdi.
    /// </remarks>
    [Fact]
    public void Content_hash_ignores_line_endings_but_changes_with_the_text()
    {
        var original = MarkdownDocumentParser.Parse(FileName, Valid).ContentHash;

        MarkdownDocumentParser.Parse(FileName, Valid.Replace("\r\n", "\n")).ContentHash.ShouldBe(original);
        MarkdownDocumentParser.Parse(FileName, Valid.Replace("30 gün", "45 gün")).ContentHash.ShouldNotBe(original);
    }
}
