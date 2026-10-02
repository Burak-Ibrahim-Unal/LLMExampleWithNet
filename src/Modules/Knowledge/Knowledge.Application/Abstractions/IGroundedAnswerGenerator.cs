namespace Knowledge.Application.Abstractions;

/// <summary>
/// Dil modeli adımının portu: soruyu yalnızca verilen kaynaklardan yanıtlar ve her bilgiyi kaynağın kısa etiketiyle
/// (C1, C2…) belirtir.
/// </summary>
/// <remarks>
/// Application katmanı LLM SDK'larını (OpenAI, Microsoft.Extensions.AI) tanıyamaz; mimari testler bunu zorlar. Bu
/// arayüz sayesinde gerçek adaptör (<c>OpenAiCompatibleAnswerGenerator</c>), model yapılandırılmamışken kullanılan
/// <c>UnconfiguredAnswerGenerator</c> ve testlerdeki <c>FakeAnswerGenerator</c> birbirinin yerine geçebilir; sağlayıcı
/// (llama.cpp, LM Studio, OpenAI…) kod değişmeden yapılandırmayla değişir. Port bilerek modelin "ham" kararını döndürür:
/// atıf doğrulama, sürüm seçimi ve öncelik denetimi modele güvenilmeden Application katmanında yapılır.
/// </remarks>
public interface IGroundedAnswerGenerator
{
    /// <summary>
    /// Bir dil modeli ucu yapılandırılmışsa true (<c>Llm:BaseUrl</c> boşsa false). Sağlık ucu "ok"/"degraded" kararında
    /// bunu da kullanır; değer yalnızca yapılandırmayı gösterir, sunucunun o an erişilebilir olduğunu ölçmez.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Yapılandırılmış sohbet modelinin adı (yapılandırılmamışsa boş); sağlık ucunda raporlanır.</summary>
    string ModelName { get; }

    /// <summary>
    /// Soruyu etiketlenmiş bağlam bölümleriyle birlikte modele gönderir ve şemaya uygun, yapılandırılmış yanıtı döndürür.
    /// </summary>
    /// <remarks>
    /// Modelin "kaynaklar yetmiyor" demesi (<c>Answerable=false</c>) bir istisna değil, geçerli bir sonuçtur. İstisna
    /// yalnızca teknik hatalarda fırlatılır; böylece handler "bilgi yok" yanıtını 503/502 hatalarından ayırabilir ve
    /// teknik bir arızayı kullanıcıya "bilgi yok" diye göstermez.
    /// </remarks>
    /// <param name="question">Kırpılmış ve iş kurallarından geçmiş soru.</param>
    /// <param name="context">Sürüm çözümünden sonra modele verilecek, C1..Cn etiketli bölümler.</param>
    /// <param name="feedback">
    /// Handler'ın düzeltme turunda verdiği geri bildirim; ilk denemede null. Doluysa adaptör, önceki yanıtın neden kabul
    /// edilmediğini modele aynı istekte söyler.
    /// </param>
    /// <param name="maxAttempts">
    /// Bu çağrıda yapılabilecek en fazla model isteği (geçersiz çıktının yeniden denenmesi dahil). Handler soru başına
    /// tek bir bütçe tutar ve adaptöre yalnızca kalanını verir; böylece adaptörün kendi yeniden denemesi ile handler'ın
    /// düzeltme turu toplanıp bütçeyi aşamaz.
    /// </param>
    /// <param name="cancellationToken">İsteğin iptal belirteci.</param>
    /// <exception cref="Exceptions.AnswerGenerationException">Modele ulaşılamıyorsa ya da model, deneme bütçesi boyunca geçerli çıktı vermediyse.</exception>
    Task<GeneratedAnswer> GenerateAsync(
        string question,
        IReadOnlyList<ContextChunk> context,
        AnswerFeedback? feedback = null,
        int maxAttempts = 2,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler'ın düzeltme turunda modele ilettiği geri bildirim: önceki yanıtın hiçbir atfı kabul edilemedi (alıntılar
/// atıf yapılan bölümün metninde birebir bulunamadı ya da atıflar verilen kaynaklara dayanmıyordu) ve/veya yanıttaki
/// çelişki kayıtları verilen kaynaklarda olmayan kimliklere işaret ediyordu.
/// </summary>
/// <remarks>
/// Sıcaklık 0 ve sabit seed ile aynı istek aynı hatalı alıntıyı yeniden üretirdi; ikinci denemenin işe yaraması için
/// modelin neyin yanlış olduğunu görmesi gerekir. Geri bildirim bilerek yalnızca veri taşır: modele gidecek Türkçe
/// talimatın metni Infrastructure katmanındaki prompt sınıfında kurulur, böylece Application katmanı prompt
/// ayrıntısına bağlanmaz.
/// </remarks>
/// <param name="UnverifiedQuotes">
/// Önceki yanıtta doğrulanamayan alıntılar, modelin yazdığı hâliyle. Atıfların hiçbiri verilen bir kaynağa
/// dayanmıyorsa ya da sorun yalnızca çelişki kimliklerindeyse boş olabilir.
/// </param>
/// <param name="CitationsRejected">
/// Önceki yanıtın hiçbir atfı kabul edilmediyse true. Atıflar kabul edildiği hâlde yalnızca çelişki kimlikleri
/// geçersizse false; o durumda model doğru alıntılarını değiştirmeye yönlendirilmez.
/// </param>
/// <param name="InvalidConflictReferences">
/// Önceki yanıtın çelişki kayıtları verilen kaynaklarda olmayan ya da eksik kimlikler içeriyorduysa true. Böyle bir kayıt
/// denetlenemez: öncelik kuralının kazananı ve kaybedeni belirlenemez.
/// </param>
public sealed record AnswerFeedback(IReadOnlyList<string> UnverifiedQuotes, bool CitationsRejected = true, bool InvalidConflictReferences = false);

/// <summary>
/// Modele verilen tek bir bağlam bölümü: indeks bölümü ve ona bu istek için atanan kısa etiket.
/// </summary>
/// <remarks>
/// Etiketleri handler sürüm çözümünden sonra sırayla verir (C1..Cn). Model uzun bir GUID ya da doküman kimliği yerine
/// kısa bir etiketi kopyalar; bu hem token tasarrufu sağlar hem de uydurma kimlik riskini azaltır. Modelin döndürdüğü
/// etiket ancak bu listedeki bir etiketle eşleşirse geçerli sayılır.
/// </remarks>
/// <param name="Label">Modele gösterilen kısa kimlik ("C1", "C2", ...); model kaynakları bu etiketle belirtir.</param>
/// <param name="Chunk">Etiketin işaret ettiği indeks bölümü (doküman, sürüm, bölüm yolu ve metin).</param>
public sealed record ContextChunk(string Label, IndexedChunk Chunk);

/// <summary>
/// Dil modelinin yapılandırılmış yanıtı, henüz doğrulanmamış hâliyle. Handler bu kaydı olduğu gibi kullanmaz: atıflar
/// <c>CitationValidator</c> ile, çelişkiler <c>SourcePrecedence</c> ile denetlenir, yanıt metni <c>AnswerText.Clean</c>
/// ile temizlenir.
/// </summary>
/// <param name="Answerable">Kapı 2: model kaynakları yeterli bulduysa true; false ise handler açık bir "bilgi yok" yanıtı döndürür.</param>
/// <param name="Answer">Müşteriye iletilecek yanıt metni; yanıtlanamıyorsa boş.</param>
/// <param name="Citations">Modelin verdiği atıflar (etiket + alıntı); geçerlilikleri ayrıca doğrulanır.</param>
/// <param name="MissingInformation">Kaynakların karşılamadığı kısım; yoksa boş dize (llama.cpp grammar uyumu için şemada null kullanılmaz).</param>
/// <param name="Conflicts">Modelin farklı dokümanlar arasında tespit ettiği çelişkiler; yoksa boş liste.</param>
/// <param name="Model">
/// Yanıtı üreten modelin yapılandırılmış adı. Sunucunun döndürdüğü model kimliği kullanılmaz: llama.cpp orada yerel model
/// dosyasının yolunu döndürür ve bu makine ayrıntısı API yanıtına sızardı.
/// </param>
/// <param name="InputTokens">Bu yanıt için yapılan bütün isteklerin toplam girdi token sayısı; sağlayıcı kullanım bilgisi döndürmezse null.</param>
/// <param name="OutputTokens">Bu yanıt için yapılan bütün isteklerin toplam çıktı token sayısı; sağlayıcı kullanım bilgisi döndürmezse null.</param>
/// <param name="Attempts">
/// Bu yanıt için sunucuya gerçekte giden model isteği sayısı (geçersiz çıktı yüzünden yapılan yeniden deneme dahil).
/// Handler soru başına çağrı bütçesini ve tanılamadaki <c>modelCalls</c> değerini bu sayıyla tutar.
/// </param>
/// <param name="LeaksSystemPrompt">
/// Modelin serbest metin alanlarından biri (yanıt, eksik bilgi açıklaması, çelişki konusu ya da gerekçesi) sistem
/// prompt'undan bir cümleyi tekrarlıyorsa true. Sistem prompt'unu yalnızca üretici bildiği için tespit üreticide yapılır;
/// handler böyle bir yanıtı göstermeden <c>UnsafeOutput</c> gerekçesiyle reddeder.
/// </param>
public sealed record GeneratedAnswer(
    bool Answerable,
    string Answer,
    IReadOnlyList<GeneratedCitation> Citations,
    string MissingInformation,
    IReadOnlyList<GeneratedConflict> Conflicts,
    string Model,
    long? InputTokens,
    long? OutputTokens,
    int Attempts = 1,
    bool LeaksSystemPrompt = false);

/// <summary>Modelin tek bir atfı: hangi kaynağa dayandığı ve o kaynaktan alıntıladığı metin.</summary>
/// <param name="ChunkLabel">
/// Etiket, modelin yazdığı hâliyle ("C1", "c1", "[C1]" ya da "1" gelebilir); karşılaştırmadan önce
/// <c>SourceLabel.Normalize</c> ile tek biçime getirilir.
/// </param>
/// <param name="Quote">Kaynaktan birebir kopyalanması istenen kısa alıntı; gerçekten geçip geçmediğini <c>CitationValidator</c> denetler.</param>
public sealed record GeneratedCitation(string ChunkLabel, string Quote);

/// <summary>
/// Modelin farklı dokümanlar arasında bildirdiği bir çelişki: hangi kaynağı seçtiği, hangilerini elediği ve neden.
/// </summary>
/// <remarks>
/// Aynı doküman ailesinin sürümleri arasındaki çelişki buraya hiç gelmez: <c>VersionResolver</c> eski sürümleri model
/// çağrılmadan eler, model her aileden yalnızca yürürlükteki sürümü görür. Burada kalan, farklı aileler arasındaki
/// (ör. politika ile SSS) çelişkilerdir; handler modelin seçiminin öncelik kuralına uyup uymadığını sunucu tarafında
/// hesaplar ve ihlalde kurala göre kaybeden bölümleri bağlamdan çıkarıp modeli yeniden çağırır.
/// </remarks>
/// <param name="Topic">Çelişkinin konusu.</param>
/// <param name="ChosenChunkLabel">Modelin geçerli kabul ettiği kaynağın etiketi.</param>
/// <param name="RejectedChunkLabels">Modelin elediği kaynakların etiketleri.</param>
/// <param name="Reason">Modelin seçim gerekçesi.</param>
public sealed record GeneratedConflict(string Topic, string ChosenChunkLabel, IReadOnlyList<string> RejectedChunkLabels, string Reason);
