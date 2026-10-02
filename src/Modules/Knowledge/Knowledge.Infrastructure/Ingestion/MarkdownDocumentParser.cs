using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;
using Knowledge.Domain.Entities;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Knowledge.Infrastructure.Ingestion;

/// <summary>
/// Tek bir bilgi tabanı dosyasını ayrıştırır: önce YAML front matter (metadata), ardından Markdown gövdesi. Sonuç,
/// ingestion'ın veritabanıyla uzlaştırdığı <see cref="SourceDocument"/>'tır.
/// </summary>
/// <remarks>
/// Zorunlu front matter alanları: <c>id</c>, <c>documentKey</c>, <c>title</c>, <c>version</c>, <c>effectiveDate</c>
/// (<c>yyyy-MM-dd</c>), <c>status</c> (<c>active</c> | <c>superseded</c>) ve <c>category</c> (<c>politika</c> |
/// <c>prosedur</c> | <c>kilavuz</c> | <c>sss</c>); <c>supersedes</c> isteğe bağlıdır ve boş değeri "yok" sayılır.
/// Sürüm seçimi (<c>VersionResolver</c>) ve kaynak önceliği (<c>SourcePrecedence</c>) bu alanlara dayandığından eksik
/// ya da geçersiz bir değer sessizce varsayılana çevrilmez: dosya adını içeren Türkçe bir
/// <see cref="KnowledgeBaseFormatException"/> fırlatılır ve ingestion bunu 422 olarak raporlar.
/// </remarks>
public static class MarkdownDocumentParser
{
    /// <summary>Front matter'ı açan ve kapatan satır.</summary>
    private const string Delimiter = "---";

    /// <summary>
    /// Front matter okuyucusu. camelCase eşleme, dosyadaki <c>documentKey</c> ve <c>effectiveDate</c> gibi anahtarları
    /// özelliklere bağlar; <c>IgnoreUnmatchedProperties</c> sayesinde tanınmayan ek alanlar (ör. yazar, etiketler) dosyayı
    /// geçersiz kılmaz. Bir kez kurulur ve her dosya için yeniden kullanılır.
    /// </summary>
    private static readonly IDeserializer FrontMatterDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Dosya metnini doğrulayıp <see cref="SourceDocument"/>'a çevirir: front matter'ı ayırıp okur, zorunlu alanları
    /// doğrular, gövdeyi bölümlere (chunk) böler ve içerik özetini (hash) hesaplar.
    /// </summary>
    /// <param name="fileName">Yalnızca hata mesajlarında kullanılır; operatöre hangi dosyanın bozuk olduğunu söyler.</param>
    /// <param name="text">Dosyanın ham içeriği; BOM ve CRLF satır sonları içerebilir.</param>
    /// <param name="maxChunkChars">Bu uzunluğu aşan bölümler paragraf sınırlarından bölünür.</param>
    /// <remarks>
    /// <c>ContentHash</c>, front matter dahil normalize edilmiş metnin SHA-256 özetidir. Özet aynıysa ingestion saklanan
    /// chunk'ları ve embedding'leri olduğu gibi kullanır (embedding sunucusu uzak ve yavaş olduğu için önemlidir);
    /// yalnızca <c>status</c> değişse bile özet değişir; doküman "değişmiş" sayılır, metadata'sı güncellenir ve bölümleri
    /// yeniden oluşturulur. Boş front matter (açılış
    /// <c>---</c> satırının hemen ardından kapanış <c>---</c>) eskiden aralık dışı bir dilimle ayrıştırıcıyı çökertiyordu;
    /// artık boş YAML olarak okunur ve "zorunlu alan eksik" hatasına dönüşür.
    /// </remarks>
    public static SourceDocument Parse(string fileName, string text, int maxChunkChars = MarkdownSectionChunker.DefaultMaxChunkChars)
    {
        // Satır sonları checkout'a bağlıdır (Windows'ta CRLF); özet normalize edilmiş metinden hesaplandığı için "doküman
        // değişmedi" tespiti makineden makineye kararlı kalır. Metin BOM'u ayıklamayan bir yoldan gelirse baştaki BOM
        // karakteri de atılır; aksi hâlde dosya '---' ile başlıyor sayılmazdı.
        var normalized = text.TrimStart('﻿').Replace("\r\n", "\n");

        if (!normalized.StartsWith(Delimiter + "\n", StringComparison.Ordinal))
        {
            throw Invalid(fileName, "dosya YAML front matter ('---') ile başlamalı");
        }

        // Kapanış satırı, açılış '---'nin bittiği konumdan (onu izleyen '\n' dahil) itibaren aranır; böylece açılışın hemen
        // ardından gelen kapanış satırı, yani boş front matter da bulunur.
        var closing = normalized.IndexOf("\n" + Delimiter + "\n", Delimiter.Length, StringComparison.Ordinal);

        if (closing < 0)
        {
            throw Invalid(fileName, "front matter kapanış satırı ('---') bulunamadı");
        }

        // "---\n---\n": kapanış satırı açılışın hemen ardından geliyor, yani front matter boş. Eskiden bu durumda dilim
        // aralığı ters düşüyor ([4..3]) ve ArgumentOutOfRangeException açılıştaki ingestion'ı çökertiyordu; artık boş YAML
        // okunur ve eksik alan hatası (422) üretilir.
        var yaml = closing > Delimiter.Length ? normalized[(Delimiter.Length + 1)..closing] : string.Empty;
        var frontMatter = ReadFrontMatter(fileName, yaml);
        // Gövde, kapanış satırının ("\n---\n", 5 karakter) hemen sonrasından başlar.
        var body = normalized[(closing + Delimiter.Length + 2)..];

        // Başlık bölümlemeden önce doğrulanır: chunker, ilk alt başlıktan önceki metne bölüm yolu olarak başlığı verir.
        var title = Required(fileName, frontMatter.Title, "title");
        var sections = MarkdownSectionChunker.Split(body, title, maxChunkChars);

        // Hiç içerik bölümü olmayan bir doküman aranamaz ve kaynak gösterilemez; sessizce atlanmak yerine reddedilir.
        if (sections.Count == 0)
        {
            throw Invalid(fileName, "içerik bölümü bulunamadı");
        }

        return new SourceDocument(
            SourceId: Required(fileName, frontMatter.Id, "id"),
            DocumentKey: Required(fileName, frontMatter.DocumentKey, "documentKey"),
            Title: title,
            Version: Required(fileName, frontMatter.Version, "version"),
            EffectiveDate: ParseDate(fileName, Required(fileName, frontMatter.EffectiveDate, "effectiveDate")),
            Status: ParseStatus(fileName, Required(fileName, frontMatter.Status, "status")),
            Category: ParseCategory(fileName, Required(fileName, frontMatter.Category, "category")),
            Supersedes: string.IsNullOrWhiteSpace(frontMatter.Supersedes) ? null : frontMatter.Supersedes.Trim(),
            ContentHash: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))),
            Sections: sections);
    }

    /// <summary>
    /// YAML metnini <see cref="FrontMatter"/>'a çevirir. Boş YAML için YamlDotNet <c>null</c> döndürdüğünden boş bir
    /// nesneye düşülür; böylece eksik alanlar tek tip "zorunlu alan eksik" hatasıyla raporlanır. Sözdizimi hataları
    /// (<c>YamlException</c>) dosya adını taşıyan <see cref="KnowledgeBaseFormatException"/>'a çevrilir; ham YAML istisnası
    /// hangi dosyanın bozuk olduğunu söylemez ve ingestion'da 422 yerine beklenmeyen bir hataya dönüşürdü.
    /// </summary>
    private static FrontMatter ReadFrontMatter(string fileName, string yaml)
    {
        try
        {
            return FrontMatterDeserializer.Deserialize<FrontMatter>(yaml) ?? new FrontMatter();
        }
        catch (YamlException exception)
        {
            throw Invalid(fileName, $"front matter okunamadı ({exception.Message})");
        }
    }

    /// <summary>
    /// Zorunlu bir front matter alanının dolu olduğunu doğrular ve kırpılmış değerini döndürür; alan eksik ya da boşsa
    /// alan adını içeren bir hata fırlatır. Varsayılan değer uydurmak yerine reddetmek bilinçlidir: <c>documentKey</c>
    /// veya <c>effectiveDate</c> gibi alanlar hangi sürümün yürürlükte sayılacağını doğrudan belirler.
    /// </summary>
    private static string Required(string fileName, string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw Invalid(fileName, $"zorunlu front matter alanı eksik: {field}")
            : value.Trim();
    }

    /// <summary>
    /// <c>effectiveDate</c> değerini yalnızca <c>yyyy-MM-dd</c> biçiminde ve kültürden bağımsız (invariant) ayrıştırır.
    /// Sürüm seçimi bu tarihe göre yapıldığından, sunucunun bölge ayarına göre farklı yorumlanabilecek <c>01.06.2025</c>
    /// gibi biçimler kabul edilmez.
    /// </summary>
    private static DateOnly ParseDate(string fileName, string value)
    {
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw Invalid(fileName, $"effectiveDate 'yyyy-MM-dd' biçiminde olmalı: {value}");
    }

    /// <summary>
    /// <c>status</c> değerini <see cref="DocumentStatus"/>'a çevirir (<c>active</c> | <c>superseded</c>). Bilinmeyen
    /// değerler reddedilir: örneğin "draft" sessizce aktif sayılsaydı, yürürlüğe girmemiş bir kural cevaplara
    /// karışabilirdi. Anahtar kelimeler ASCII olduğundan küçük harfe çevirme bilinçli olarak invariant kültürle yapılır
    /// (tr-TR kurallarıyla "ACTIVE" → "actıve" olurdu).
    /// </summary>
    private static DocumentStatus ParseStatus(string fileName, string value)
    {
        return value.ToLowerInvariant() switch
        {
            "active" => DocumentStatus.Active,
            "superseded" => DocumentStatus.Superseded,
            _ => throw Invalid(fileName, $"bilinmeyen status: {value} (active | superseded)")
        };
    }

    /// <summary>
    /// <c>category</c> değerini <see cref="DocumentCategory"/>'ye çevirir (<c>politika</c> | <c>prosedur</c> |
    /// <c>kilavuz</c> | <c>sss</c>). Kategori, kaynaklar çeliştiğinde hangisinin üstün geleceğini belirler (politika ve
    /// prosedür &gt; kılavuz &gt; SSS), bu yüzden bilinmeyen değerler reddedilir. Değerler front matter'da Türkçe karaktersiz
    /// yazılır; invariant küçük harf dönüşümü "KILAVUZ" gibi büyük harfli yazımı da doğru eşler.
    /// </summary>
    private static DocumentCategory ParseCategory(string fileName, string value)
    {
        return value.ToLowerInvariant() switch
        {
            "politika" => DocumentCategory.Policy,
            "prosedur" => DocumentCategory.Procedure,
            "kilavuz" => DocumentCategory.Guide,
            "sss" => DocumentCategory.Faq,
            _ => throw Invalid(fileName, $"bilinmeyen category: {value} (politika | prosedur | kilavuz | sss)")
        };
    }

    /// <summary>
    /// "dosya: neden" biçiminde bir <see cref="KnowledgeBaseFormatException"/> oluşturur. Ingestion bu mesajı 422
    /// yanıtında olduğu gibi gösterdiğinden operatör bozuk dosyayı ve sorunu doğrudan görür.
    /// </summary>
    private static KnowledgeBaseFormatException Invalid(string fileName, string reason) => new($"{fileName}: {reason}");

    /// <summary>
    /// Front matter'ın ham hâli. Bütün alanlar bilinçli olarak null olabilen <c>string</c>'dir: tarih ve enum dönüşümleri
    /// YAML kütüphanesine bırakılmaz, yukarıdaki yardımcılarla yapılır; böylece eksik veya hatalı her alan genel bir YAML
    /// tip hatası yerine alan adını söyleyen Türkçe bir mesajla raporlanır.
    /// </summary>
    private sealed class FrontMatter
    {
        /// <summary>Sürüme özgü kararlı doküman kimliği, ör. <c>iade-politikasi-v2</c>.</summary>
        public string? Id { get; set; }

        /// <summary>Doküman ailesi; aynı prosedürün sürümleri bu anahtarı paylaşır, ör. <c>iade-politikasi</c>.</summary>
        public string? DocumentKey { get; set; }

        /// <summary>Doküman başlığı; kaynak gösteriminde ve ilk alt başlıktan önceki metnin bölüm yolunda kullanılır.</summary>
        public string? Title { get; set; }

        /// <summary>Sürüm etiketi, ör. <c>2.0</c>; aynı yürürlük tarihli sürümler arasında ikincil sıralama ölçütüdür.</summary>
        public string? Version { get; set; }

        /// <summary>Yürürlük tarihi (<c>yyyy-MM-dd</c>); sürüm seçiminin ana ölçütüdür.</summary>
        public string? EffectiveDate { get; set; }

        /// <summary><c>active</c> veya <c>superseded</c>; <c>superseded</c> işaretli sürüm tarihi ne olursa olsun seçilmez.</summary>
        public string? Status { get; set; }

        /// <summary>Bu sürümün yerini aldığı dokümanın kimliği; isteğe bağlıdır, boş değer "yok" sayılır.</summary>
        public string? Supersedes { get; set; }

        /// <summary><c>politika</c>, <c>prosedur</c>, <c>kilavuz</c> veya <c>sss</c>; kaynak önceliğini belirler.</summary>
        public string? Category { get; set; }
    }
}
