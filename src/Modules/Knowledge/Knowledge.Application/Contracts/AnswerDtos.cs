namespace Knowledge.Application.Contracts;

/// <summary>
/// <c>POST /v1/questions</c> yanıtının gövdesi. Yanıtlanan ve reddedilen sorular aynı tiple ve HTTP 200 ile döner:
/// "dokümanlarda yeterli bilgi yok" geçerli bir iş sonucudur, hata değildir.
/// </summary>
/// <remarks>
/// Ödevin şartları doğrudan bu sözleşmeye yansır: kullanılan doküman ve bölüm <c>Sources</c>'ta, bilgi yetersizliği
/// <c>Answerable=false</c> ve <c>RefusalReason</c>'da, güncel sürümün nasıl seçildiği <c>VersionResolution</c>'da,
/// farklı dokümanlar arasındaki çelişkiler <c>Conflicts</c>'ta görünür. <c>Diagnostics</c> değerlendirme ve hata ayıklama
/// içindir. Aynı nesne JSON olarak soru loguna da yazılır.
/// </remarks>
/// <param name="Question">Sorulan soru (baştaki/sondaki boşluklar kırpılmış).</param>
/// <param name="Answerable">Dokümanlar yeterli bilgi içermiyorsa false; <paramref name="Answer"/> bu durumda bunu açıkça söyler.</param>
/// <param name="Answer">Yanıt metni; retlerde sabit Türkçe "yeterli bilgi bulunamadı" mesajı.</param>
/// <param name="Sources">
/// Yanıtın dayandığı doküman ve bölümler, alıntılanan metinle birlikte; yalnızca alıntısı bölüm metninde doğrulanmış
/// atıflar listelenir. Retlerde boş.
/// </param>
/// <param name="VersionResolution">Aynı dokümanın çakışan sürümlerinin nasıl çözüldüğü.</param>
/// <param name="Conflicts">
/// Farklı dokümanlar arasında modelin bildirdiği ve sunucunun öncelik kuralına göre denetleyip gerekirse düzelttiği
/// anlaşmazlıklar.
/// </param>
/// <param name="MissingInformation">Kaynakların karşılamadığı kısım (kısmi yanıtlarda ve retlerde).</param>
/// <param name="RefusalReason">
/// Yanıtlandıysa boş; aksi hâlde LowRelevance, NoSourceInEffect, ModelInsufficientContext, NoValidCitations veya
/// UnresolvedConflict.
/// </param>
/// <param name="Diagnostics">Arama modu, skorlar, modele verilen bağlam, model adı ve gecikme gibi teşhis bilgileri.</param>
public sealed record AnswerDto(
    string Question,
    bool Answerable,
    string Answer,
    IReadOnlyList<AnswerSourceDto> Sources,
    VersionResolutionDto VersionResolution,
    IReadOnlyList<ConflictDto> Conflicts,
    string MissingInformation,
    string RefusalReason,
    AnswerDiagnosticsDto Diagnostics);

/// <summary>
/// Yanıtın dayandığı tek bir kaynak: doküman, sürüm, yürürlük tarihi, bölüm ve alıntı. "Her yanıt kullanılan doküman ve
/// ilgili bölümü gösterir" şartını karşılar.
/// </summary>
/// <param name="DocumentId">Sürüme özgü doküman kimliği (ör. "iade-politikasi-v2").</param>
/// <param name="Title">Doküman başlığı; kullanıcının kaynağı tanıması için.</param>
/// <param name="Version">Kullanılan doküman sürümü; yanıtın hangi sürüme dayandığını görünür kılar.</param>
/// <param name="EffectiveDate">Kullanılan sürümün yürürlük tarihi.</param>
/// <param name="Status">"active" veya "superseded" (front matter'daki sözcüklerle).</param>
/// <param name="Category">"politika", "prosedur", "kilavuz" veya "sss".</param>
/// <param name="Section">Bölüm yolu (ör. "2. İade Süresi").</param>
/// <param name="Quote">Modelin bu bölümden yaptığı alıntı.</param>
/// <param name="QuoteVerified">
/// Alıntı bölüm metninde geçiyorsa, yani başka sözcüklerle ifade edilmemiş ya da uydurulmamışsa true. Yanıtta yalnızca
/// doğrulanmış alıntılı kaynaklar listelendiği için listelenen her kaynakta true'dur; alan, sözleşmenin açık kalması ve
/// istemcinin bunu kendisi de denetleyebilmesi için taşınır.
/// </param>
public sealed record AnswerSourceDto(
    string DocumentId,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    string Status,
    string Category,
    string Section,
    string Quote,
    bool QuoteVerified);

/// <summary>
/// Aynı dokümanın sürümleri arasındaki çakışmanın nasıl çözüldüğünü açıklar ("kaynaklar çeliştiğinde güncel sürümün nasıl
/// seçildiğini göster" şartı).
/// </summary>
/// <remarks>
/// Yalnızca yanıtın atıf yaptığı doküman aileleri raporlanır; ilgisiz bir ailedeki sürüm kararı yanıtı açıklamaz, yalnızca
/// gürültü olurdu. Retlerde seçim ve eleme listeleri boştur, çünkü sürüm kararları bir yanıtın kaynaklarını açıklar; arama
/// ve bağlam ayrıntısı her durumda <c>Diagnostics</c> altındadır.
/// </remarks>
/// <param name="Applied">
/// Soruyla eşleşen eski (ya da henüz yürürlüğe girmemiş) bir sürüm elenip yerine yürürlükteki sürüm kullanıldıysa true.
/// </param>
/// <param name="Rule">Uygulanan seçim kuralının Türkçe metni.</param>
/// <param name="Selected">Yanıtın dayandığı çok sürümlü aileler için seçilen (yürürlükteki) sürümler.</param>
/// <param name="Discarded">Elenen sürümler ve gerekçeleri.</param>
public sealed record VersionResolutionDto(
    bool Applied,
    string Rule,
    IReadOnlyList<VersionRefDto> Selected,
    IReadOnlyList<DiscardedVersionDto> Discarded);

/// <summary>Seçilen bir sürüme kısa başvuru: kimlik, başlık, sürüm ve yürürlük tarihi.</summary>
public sealed record VersionRefDto(string DocumentId, string Title, string Version, DateOnly EffectiveDate);

/// <summary>Elenen bir sürüm ve neden kullanılmadığı.</summary>
/// <param name="DocumentId">Elenen sürümün doküman kimliği.</param>
/// <param name="Title">Elenen sürümün başlığı.</param>
/// <param name="Version">Elenen sürümün numarası.</param>
/// <param name="EffectiveDate">Elenen sürümün yürürlük tarihi; gelecekteyse eleme gerekçesi budur.</param>
/// <param name="Reason">Türkçe gerekçe, ör. "2.0 sürümü (2025-06-01) tarafından geçersiz kılındı."</param>
public sealed record DiscardedVersionDto(string DocumentId, string Title, string Version, DateOnly EffectiveDate, string Reason);

/// <summary>
/// Farklı dokümanlar arasında modelin bildirdiği bir çelişki ve sunucunun bu seçimi öncelik kuralına göre denetleme sonucu.
/// </summary>
/// <remarks>
/// Yalnızca seçilen kaynağı ve en az bir elenen kaynağı modele verilen bağlamda bulunan çelişkiler listelenir; bağlam
/// dışı etiketlere dayanan bildirimler atılır. Model kurala aykırı bir kaynağı seçtiyse sunucu kaybeden bölümleri
/// bağlamdan çıkarıp modeli yeniden çağırır; kayıt bu durumda sunucunun kararını gösterir (seçilen kuralın kazananı,
/// gerekçe sunucunun kuralı uyguladığını söyleyen metin). Yalnızca seçilen kaynağı yanıtın atıf yaptığı dokümanlar
/// arasında olan kayıtlar gösterilir: yanıtın dayanmadığı bir çelişki yanıtı açıklamaz.
/// </remarks>
/// <param name="Topic">Çelişkinin konusu.</param>
/// <param name="Chosen">Geçerli kabul edilen kaynak: modelin seçimi ya da sunucu düzelttiyse kuralın kazananı.</param>
/// <param name="Rejected">Elenen kaynaklar.</param>
/// <param name="Reason">Modelin seçim gerekçesi ya da sunucu düzelttiyse uygulanan kuralın metni.</param>
/// <param name="RuleSatisfied">
/// Seçim öncelik kuralına (önce yetki, sonra tazelik) uyuyorsa true; değeri sunucu hesaplar. Başarılı bir yanıtta her
/// zaman true'dur: ihlal ya düzeltme turunda giderilir ya da yanıt <c>UnresolvedConflict</c> ile reddedilir.
/// </param>
public sealed record ConflictDto(
    string Topic,
    ConflictSourceDto Chosen,
    IReadOnlyList<ConflictSourceDto> Rejected,
    string Reason,
    bool RuleSatisfied);

/// <summary>Bir çelişkideki kaynak; öncelik kararının dayandığı tür ve yürürlük tarihiyle birlikte.</summary>
/// <param name="DocumentId">Kaynağın doküman kimliği.</param>
/// <param name="Version">Kaynağın sürümü.</param>
/// <param name="EffectiveDate">Kaynağın yürürlük tarihi; türler eşitse daha yeni olan üstün gelir.</param>
/// <param name="Category">"politika", "prosedur", "kilavuz" veya "sss"; öncelik sırasını belirler.</param>
/// <param name="Section">Bölüm yolu.</param>
public sealed record ConflictSourceDto(string DocumentId, string Version, DateOnly EffectiveDate, string Category, string Section);

/// <summary>
/// Yanıtın nasıl üretildiğine dair teşhis bilgileri. Değerlendirme ve hata ayıklama için vardır: bir sorunun aramada mı
/// yoksa üretimde mi başarısız olduğunu ayırt etmeyi sağlar.
/// </summary>
/// <param name="RetrievalMode">Bu soruda kullanılan arama modu: "hybrid" veya "lexical".</param>
/// <param name="MaxDenseScore">Kapı 1'in gördüğü en iyi kosinüs benzerliği (lexical modda 0).</param>
/// <param name="MaxLexicalCoverage">Kapı 1'in gördüğü en iyi kelime kapsamı (0..1).</param>
/// <param name="CandidateDocumentIds">Sürüm çözümünden önce aramanın bulduğu dokümanlar.</param>
/// <param name="Context">
/// Modele son çağrıda verilen bölümler, etiketleriyle (sürüm çözümünden sonra; çelişki düzeltmesi yapıldıysa kaybeden
/// bölümler çıkarılmış hâliyle).
/// </param>
/// <param name="Model">
/// Yapılandırılmış sohbet modelinin adı; model çağrılmadıysa boş. Sunucunun döndürdüğü kimlik kullanılmaz, çünkü llama.cpp
/// orada yerel model dosyasının yolunu döndürür ve bu makine ayrıntısı yanıta sızardı.
/// </param>
/// <param name="LatencyMs">Aramanın başlangıcından son model yanıtının alınmasına (model çağrılmadıysa ret anına) kadar geçen süre, milisaniye.</param>
/// <param name="InputTokens">Tüm model çağrılarının toplam girdi token sayısı; model çağrılmadıysa ya da sağlayıcı bildirmediyse null.</param>
/// <param name="OutputTokens">Tüm model çağrılarının toplam çıktı token sayısı; model çağrılmadıysa ya da sağlayıcı bildirmediyse null.</param>
/// <param name="ModelCalls">
/// Bu soru için dil modelinin kaç kez çağrıldığı: 0 (model çağrılmadan verilen ret), 1 ya da düzeltme turu yapıldıysa 2.
/// Gecikmenin neden yüksek olduğunu ve düzeltme turunun ne sıklıkla gerektiğini görünür kılar.
/// </param>
public sealed record AnswerDiagnosticsDto(
    string RetrievalMode,
    double MaxDenseScore,
    double MaxLexicalCoverage,
    IReadOnlyList<string> CandidateDocumentIds,
    IReadOnlyList<ContextSourceDto> Context,
    string Model,
    long LatencyMs,
    long? InputTokens,
    long? OutputTokens,
    int ModelCalls);

/// <summary>Modele verilen bir bağlam bölümü: etiketi (C1..Cn), dokümanı, sürümü ve bölüm yolu.</summary>
/// <remarks>Etiket, modelin atıflarda kullandığı kimliktir; atıfları ve çelişkileri bağlamla eşleştirerek incelemeye yarar.</remarks>
public sealed record ContextSourceDto(string Label, string DocumentId, string Version, string Section);
