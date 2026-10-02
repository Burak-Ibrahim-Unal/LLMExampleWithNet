using System.ComponentModel;

namespace Knowledge.Infrastructure.Llm;

// Modele JSON şeması olarak gönderilen yapılandırılmış çıktı sözleşmesi (bu dosyadaki üç sınıf). Hiçbir özellik null
// olamaz: llama.cpp şemayı bir grammar'a çevirir ve sade türler (null yerine boş metin / boş liste) en güvenilir
// biçimde dönüşür. [Description] metinleri şemaya alan açıklaması olarak girer; bu yüzden Türkçedir ve modele hitap eder.

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
    public bool Answerable { get; set; }

    /// <summary>
    /// Temsilciye gösterilecek yanıt metni. Sunucu, içine kaçmış atıf işaretlerini ve tırnak içine kopyalanmış uzun
    /// alıntıları <c>AnswerText.Clean</c> ile temizler; <see cref="Answerable"/> <c>true</c> iken boş gelmesi yeniden
    /// denemeyi tetikler.
    /// </summary>
    [Description("Temsilcinin müşteriye iletebileceği kısa Türkçe yanıt. Kaynak kimliği içermez. Yanıtlanamıyorsa boş.")]
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Yanıtı destekleyen atıflar. Kapı 3'te yalnızca etiketi modele verilen kaynaklardan birine (C1..Cn) karşılık
    /// gelen atıflar tutulur; hiçbiri kalmazsa yanıt <c>NoValidCitations</c> ile reddedilir. Her bilginin kaynağını
    /// (doküman ve bölüm) gösterme gereksinimi bu alana dayanır.
    /// </summary>
    [Description("Yanıttaki her bilgiyi destekleyen kaynaklar.")]
    public List<CitationPayload> Citations { get; set; } = [];

    /// <summary>
    /// Sorunun kaynaklarla yanıtlanamayan kısmı. Hem yanıtlarda hem de model çağrıldıktan sonra verilen retlerde
    /// yanıtın <c>missingInformation</c> alanında temsilciye gösterilir; böylece "bilgi yok" yanıtı neyin eksik
    /// olduğunu da söyler.
    /// </summary>
    [Description("Kaynaklarda bulunmayan, sorunun yanıtlanamayan kısmı; yoksa boş.")]
    public string MissingInformation { get; set; } = string.Empty;

    /// <summary>
    /// Modelin farklı dokümanlar arasında tespit ettiği çelişkiler. Sunucu her kaydı verilen kaynaklarla eşler ve
    /// seçimin öncelik kuralına uyup uymadığını kendisi hesaplar (<c>ruleSatisfied</c>); modelin beyanına körü körüne
    /// güvenilmez.
    /// </summary>
    [Description("Kaynaklar arasında tespit edilen çelişkiler; yoksa boş liste.")]
    public List<ConflictPayload> Conflicts { get; set; } = [];
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
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>
    /// Kaynaktan birebir kopyalanması istenen kısa alıntı. Sunucu alıntının bölüm metninde gerçekten geçip geçmediğini
    /// büyük/küçük harf ve Türkçe karakterden bağımsız olarak denetler ("…" ile kısaltılmış alıntıları parça parça) ve
    /// sonucu kaynak başına <c>quoteVerified</c> olarak raporlar.
    /// </summary>
    [Description("Kaynaktan birebir kopyalanmış kısa alıntı.")]
    public string Quote { get; set; } = string.Empty;
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
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Geçerli sayılan kaynağın etiketi. Modele verilen kaynaklardan birine karşılık gelmiyorsa sunucu bu çelişki
    /// kaydını yok sayar.
    /// </summary>
    [Description("Geçerli kabul edilen kaynağın kimliği.")]
    public string ChosenChunkId { get; set; } = string.Empty;

    /// <summary>
    /// Elenen kaynakların etiketleri. Sunucu yalnızca verilen kaynaklara ait olanları tutar ve seçilen kaynağın
    /// bunların her birine öncelik kuralına göre üstün olup olmadığını <c>ruleSatisfied</c> olarak hesaplar; geçerli
    /// elenen etiket kalmazsa kayıt yok sayılır.
    /// </summary>
    [Description("Elenen kaynakların kimlikleri.")]
    public List<string> RejectedChunkIds { get; set; } = [];

    /// <summary>
    /// Modelin seçim gerekçesi; temsilciye olduğu gibi gösterilir. Sunucunun kural denetimi bu metne değil, kaynakların
    /// türüne ve yürürlük tarihine dayanır.
    /// </summary>
    [Description("Seçim gerekçesi.")]
    public string Reason { get; set; } = string.Empty;
}
