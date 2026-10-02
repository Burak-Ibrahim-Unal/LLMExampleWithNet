using System.ComponentModel;

namespace Knowledge.Infrastructure.Llm;

// Modele JSON şeması olarak gönderilen yapılandırılmış çıktı sözleşmesi (bu dosyadaki üç sınıf). Her özellik zorunludur
// (required) ve hiçbiri null olamaz: llama.cpp şemayı bir grammar'a çevirir, zorunlu alanlar çıktıda atlanamaz ve sade
// türler (null yerine boş metin / boş liste) en güvenilir biçimde dönüşür. [Description] metinleri şemaya alan açıklaması
// olarak girer; bu yüzden Türkçedir ve modele hitap eder.

// TODO(halüsinasyon-2): İddia başına atıf. Bu şema yanıtı tek bir metin (Answer) ve ondan bağımsız atıflar (Citations)
// olarak alır; sunucu alıntıları doğrular ama metindeki her iddianın bir alıntıya dayandığını bilemez. Şema, her biri
// kendi alıntısını taşıyan bir iddia listesine (ör. Claims: [{ Text, ChunkId, Quote }]) dönüşebilir: sunucu her iddiayı
// CitationValidator ile doğrular, desteksiz iddiaları düşürür ya da düzeltme turuna gönderir ve yanıt metnini yalnızca
// doğrulanmış iddialardan kurar. Prompt (AnswerPrompt), şema, handler ve değerlendirme birlikte değişir; değerlendirme
// yeniden koşulmalıdır. Bkz. README, Halüsinasyon.

/// <summary>
/// Modelden istenen yanıtın kök nesnesi. <see cref="OpenAiCompatibleAnswerGenerator"/> bu türden JSON şeması üretir ve
/// model yanıtını bu türe ayrıştırır. Alanlar cevaplama hattının kapılarına karşılık gelir: <see cref="Answerable"/>
/// Kapı 2'dir, <see cref="Citations"/> Kapı 3'te (<c>CitationValidator</c>) denetlenir, <see cref="Conflicts"/> sunucuda
/// <c>SourcePrecedence</c> ile kontrol edilir.
/// </summary>
/// <remarks>
/// <para>
/// Null yerine boş metin ve boş liste kullanılır: llama.cpp'nin şema→grammar dönüşümünde sade türler en güvenilir
/// sonucu verir ve aşağı akıştaki kod null yerine yalnızca boşluk kontrolü yapar.
/// </para>
/// <para>
/// Bütün özellikler <c>required</c> işaretlidir. Bunun iki etkisi vardır: üretilen şema her alanı zorunlu listeler,
/// böylece grammar modelin bir alanı atlamasına izin vermez; ayrıştırmada da eksik bir alan hata olur. İşaret olmasaydı
/// <c>{}</c> gibi eksik bir çıktı C# varsayılanlarıyla dolar ve <c>answerable=false</c> görünerek modelin bilinçli bir
/// "bilgi yok" kararı sanılırdı; oysa bu, düzeltme denemesine gitmesi gereken bozuk bir çıktıdır.
/// </para>
/// <para>
/// <see cref="DescriptionAttribute"/> metinleri şemada alan açıklaması (<c>description</c>) olur. Prompt, kaynaklar ve
/// yanıt Türkçe olduğu için açıklamalar da Türkçedir. Grammar yalnızca yapıyı zorlar; açıklamalar ise şemayı modele
/// ileten sağlayıcılarda ve <c>UseJsonSchema=false</c> modunda (şema prompt'a eklenir) modele her alanın ne anlama
/// geldiğini anlatır. Bu metinler kodun parçasıdır: değiştirilmeleri modelin davranışını değiştirebilir.
/// </para>
/// </remarks>
public sealed class AnswerPayload
{
    /// <summary>
    /// Modelin "kaynaklar yeterli mi" kararı (Kapı 2). <c>false</c> ise yanıt, model ne yazmış olursa olsun sabit
    /// "bilgi yok" mesajıyla ve <c>ModelInsufficientContext</c> gerekçesiyle döner.
    /// </summary>
    [Description("Kaynaklar soruyu yanıtlamaya yetiyorsa true, yetmiyorsa false.")]
    public required bool Answerable { get; set; }

    /// <summary>
    /// Temsilciye gösterilecek yanıt metni. Sunucu, içine kaçmış atıf işaretlerini ve tırnak içine kopyalanmış uzun
    /// alıntıları <c>AnswerText.Clean</c> ile temizler; <see cref="Answerable"/> <c>true</c> iken boş gelmesi yeniden
    /// denemeyi tetikler.
    /// </summary>
    [Description("Temsilcinin müşteriye iletebileceği kısa Türkçe yanıt. Kaynak kimliği içermez. Yanıtlanamıyorsa boş.")]
    public required string Answer { get; set; }

    /// <summary>
    /// Yanıtı destekleyen atıflar. Kapı 3'te yalnızca etiketi modele verilen kaynaklardan birine (C1..Cn) karşılık gelen
    /// ve alıntısı o bölümde birebir geçen atıflar yanıtın kaynağı olur; hiçbiri kalmazsa model bir kez düzeltme
    /// talimatıyla yeniden çağrılır, yine olmazsa yanıt <c>NoValidCitations</c> ile reddedilir. Her bilginin kaynağını
    /// (doküman ve bölüm) gösterme gereksinimi bu alana dayanır.
    /// </summary>
    [Description("Yanıttaki her bilgiyi destekleyen kaynaklar.")]
    public required List<CitationPayload> Citations { get; set; }

    /// <summary>
    /// Sorunun kaynaklarla yanıtlanamayan kısmı. Hem yanıtlarda hem de model çağrıldıktan sonra verilen retlerde
    /// yanıtın <c>missingInformation</c> alanında temsilciye gösterilir; böylece "bilgi yok" yanıtı neyin eksik
    /// olduğunu da söyler.
    /// </summary>
    [Description("Kaynaklarda bulunmayan, sorunun yanıtlanamayan kısmı; yoksa boş.")]
    public required string MissingInformation { get; set; }

    /// <summary>
    /// Modelin farklı dokümanlar arasında tespit ettiği çelişkiler. Sunucu her kaydı verilen kaynaklarla eşler,
    /// seçimin öncelik kuralına uyup uymadığını kendisi hesaplar (<c>ruleSatisfied</c>) ve ihlalde kuralı zorlar: kurala
    /// göre kaybeden bölümler bağlamdan çıkarılıp model yeniden çağrılır. Modelin beyanına körü körüne güvenilmez.
    /// </summary>
    [Description("Kaynaklar arasında tespit edilen çelişkiler; yoksa boş liste.")]
    public required List<ConflictPayload> Conflicts { get; set; }
}

/// <summary>
/// Tek bir atıf: modelin yanıttaki bir bilgiyi dayandırdığı kaynağın etiketi ve o kaynaktan kısa bir alıntı. Bu çift,
/// her yanıtta kullanılan doküman ve bölümün gösterilmesini sağlar.
/// </summary>
public sealed class CitationPayload
{
    /// <summary>
    /// Atıf yapılan kaynağın etiketi (C1..Cn). Modeller "C1", "c1", "[C1]" veya "1" gibi varyantlar üretebildiği için
    /// sunucu etiketi normalize eder; modele verilen kaynaklardan birine karşılık gelmeyen atıf atılır.
    /// </summary>
    [Description("Kaynak kimliği, örneğin C1.")]
    public required string ChunkId { get; set; }

    /// <summary>
    /// Kaynaktan birebir kopyalanması istenen kısa alıntı. Sunucu alıntının bölüm metninde gerçekten geçip geçmediğini
    /// büyük/küçük harf ve Türkçe karakterden bağımsız olarak denetler ("…" ile kısaltılmış alıntılarda parçaları
    /// kaynaktaki sırasıyla). Doğrulanamayan alıntının atfı yanıtın kaynağı olamaz.
    /// </summary>
    [Description("Kaynaktan birebir kopyalanmış kısa alıntı.")]
    public required string Quote { get; set; }
}

/// <summary>
/// Modelin farklı dokümanlar arasında bulduğu bir çelişki ve hangi kaynağı neden seçtiği. Aynı doküman ailesinin
/// sürümleri arasındaki çelişkiyi model hiç görmez (<c>VersionResolver</c> eski sürümleri prompt'a girmeden eler);
/// bu kayıt yalnızca farklı dokümanlar (ör. SSS ile politika) arasındaki çelişkiler içindir.
/// </summary>
public sealed class ConflictPayload
{
    /// <summary>Çelişkinin konusu; yanıtın <c>conflicts</c> listesinde olduğu gibi gösterilir.</summary>
    [Description("Çelişkinin konusu.")]
    public required string Topic { get; set; }

    /// <summary>
    /// Geçerli sayılan kaynağın etiketi. Modele verilen kaynaklardan birine karşılık gelmiyorsa sunucu bu çelişki
    /// kaydını yok sayar.
    /// </summary>
    [Description("Geçerli kabul edilen kaynağın kimliği.")]
    public required string ChosenChunkId { get; set; }

    /// <summary>
    /// Elenen kaynakların etiketleri. Sunucu yalnızca verilen kaynaklara ait olanları tutar ve seçilen kaynağın
    /// bunlardan birine öncelik kuralına göre yenilip yenilmediğini hesaplar (<c>ruleSatisfied</c>); geçerli elenen etiket
    /// kalmazsa kayıt yok sayılır.
    /// </summary>
    [Description("Elenen kaynakların kimlikleri.")]
    public required List<string> RejectedChunkIds { get; set; }

    /// <summary>
    /// Modelin seçim gerekçesi; temsilciye olduğu gibi gösterilir. Sunucunun kural denetimi bu metne değil, kaynakların
    /// türüne ve yürürlük tarihine dayanır.
    /// </summary>
    [Description("Seçim gerekçesi.")]
    public required string Reason { get; set; }
}
