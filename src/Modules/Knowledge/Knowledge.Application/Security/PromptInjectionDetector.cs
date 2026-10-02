using System.Text.RegularExpressions;
using Knowledge.Application.Text;

namespace Knowledge.Application.Security;

/// <summary>
/// Bir metinde dil modelinin talimatlarını değiştirmeye yönelik kalıpları (prompt injection) arar: talimatları yok
/// saydırma, sistem prompt'unu açıklatma, rol değiştirme, jailbreak terimleri ve sohbet şablonu belirteçleri. Soru
/// tarafında modele gitmeden önce, ingest tarafında da bilgi tabanı dokümanları için kullanılır; prompt kurulurken
/// doküman metnindeki şablon belirteçleri de <see cref="RemoveChatTemplateTokens"/> ile silinir.
/// </summary>
/// <remarks>
/// <para>
/// Bu bir ilk savunma hattıdır, tek savunma değildir (OWASP LLM01): asıl güvence yapısaldır. Model yalnızca verilen
/// kaynaklardan yanıt verebilir, her yanıt alıntısı bölüm metninde doğrulanmış atıflara dayanmak zorundadır ve sürüm
/// ile öncelik kararları kodda verilir. Dedektör, bu yapıya rağmen modeli zorlamaya çalışan istekleri hiç çağrı
/// yapmadan ayıklar ve denetim kaydında görünür kılar.
/// </para>
/// <para>
/// Kalıplar sözcük değil niyet arar ve yanlış alarmı önlemek için dar tutulur: "talimat" sözcüğü tek başına yetmez,
/// "önceki / tüm / yukarıdaki" gibi bir niteleyici ve "yok say / unut / görmezden gel" gibi bir emir kipi birlikte
/// aranır. Böylece "Kurulum talimatlarını unuttum" gibi gerçek sorular yakalanmaz. Doğal dil kalıpları
/// <see cref="TurkishTextNormalizer"/> ile normalleştirilmiş metinde aranır; Türkçe karakter, büyük/küçük harf ve
/// noktalama farkı atlatma yolu olamaz. Şablon belirteçleri ise noktalama içerdikleri için ham metinde aranır.
/// </para>
/// <para>
/// Satır başındaki rol işaretleri (<c>Sistem:</c>, <c>Asistan:</c>) bilerek aranmaz: destek temsilcileri müşteri
/// kayıtlarını ("Sistem: Android 14") ve sohbet dökümlerini soruya yapıştırabilir. Bu işaretler prompt'ta zaten
/// etkisizleştirilir (Infrastructure'daki <c>AnswerPrompt.Neutralize</c>); dedektörde aranmaları yalnızca gerçek
/// soruları reddederdi. Aynı nedenle "geliştirici modu" tek başına değil, kural ya da kısıtlama sözcükleriyle birlikte
/// aranır ve "DAN modu" yalnızca büyük harfle yazılmış hâliyle tanınır: normalleştirme "Alexa'dan" ekini ayrı bir
/// sözcüğe böler ve "Alexa'dan modu…" sorusu yakalanırdı.
/// </para>
/// </remarks>
public static partial class PromptInjectionDetector
{
    /// <summary>
    /// Metinde bir prompt injection kalıbı arar ve eşleşen kuralın kısa adını döndürür; eşleşme yoksa null.
    /// </summary>
    /// <remarks>
    /// Kural adı yalnızca loglama ve testler içindir; istemciye hangi kalıbın yakalandığı söylenmez. Saldırgana geri
    /// bildirim vermek, kalıpların etrafından dolaşmayı kolaylaştırırdı.
    /// </remarks>
    /// <param name="text">Kullanıcının sorusu ya da bir doküman metni.</param>
    public static string? Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Sohbet şablonu belirteçleri ve büyük harfli "DAN" ham metinde aranır: normalleştirme belirteçleri oluşturan
        // noktalamayı (<, |) siler, büyük/küçük harf farkını ve kesme işaretini de ortadan kaldırır.
        if (ChatTemplateToken().IsMatch(text))
        {
            return "chat-template-token";
        }

        if (DanMode().IsMatch(text))
        {
            return "suspicious-term";
        }

        var normalized = TurkishTextNormalizer.Normalize(text);

        if (InstructionTarget().IsMatch(normalized) && DismissalVerb().IsMatch(normalized))
        {
            return "override-instructions";
        }

        if (SuspiciousTerm().IsMatch(normalized))
        {
            return "suspicious-term";
        }

        if (DeveloperMode().IsMatch(normalized) && RuleWord().IsMatch(normalized))
        {
            return "developer-mode";
        }

        if (EnglishOverride().IsMatch(normalized))
        {
            return "override-instructions-en";
        }

        if (RoleSwitch().IsMatch(normalized))
        {
            return "role-switch";
        }

        if (TurkishRoleSwitch().IsMatch(normalized))
        {
            return "role-switch-tr";
        }

        return null;
    }

    /// <summary>
    /// Metindeki sohbet şablonu belirteçlerini (<see cref="ChatTemplateToken"/>) birer boşlukla değiştirir; metnin geri
    /// kalanına dokunmaz. Doküman metni modele gönderilmeden önce bu yöntemden geçer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// llama.cpp sohbet şablonunu uyguladıktan sonra metni özel belirteçleri tanıyarak böler; mesaj içinde kalan bir
    /// <c>&lt;|turn&gt;</c> gerçek bir sıra belirtecine dönüşür ve sahte bir sistem ya da model sırası açabilir. Şüpheli
    /// dokümanlar indekste kaldığı için (bkz. ingest), bu belirteçlerin modele hiç ulaşmaması gerekir.
    /// </para>
    /// <para>
    /// Belirteç silinmek yerine boşlukla değiştirilir ve eşleşme kalmayana kadar tekrarlanır: "&lt;|tur&lt;|turn&gt;n&gt;"
    /// gibi iç içe bir yazım, düz silmede "&lt;|turn&gt;" bırakırdı; boşluk ise belirteç kalıplarının hiçbirinde geçemez.
    /// Her tur en az bir "&lt;" ya da "[" karakterini tükettiği için döngü sonludur.
    /// </para>
    /// </remarks>
    /// <param name="text">Modele gönderilecek güvenilmez metin (doküman başlığı, bölüm yolu, bölüm metni).</param>
    public static string RemoveChatTemplateTokens(string text)
    {
        while (ChatTemplateToken().IsMatch(text))
        {
            text = ChatTemplateToken().Replace(text, " ");
        }

        return text;
    }

    /// <summary>
    /// Sohbet şablonlarının özel belirteçleri: Gemma 4'ün asimetrik belirteçleri (<c>&lt;|turn&gt;</c>, <c>&lt;turn|&gt;</c>,
    /// <c>&lt;|channel&gt;</c>, <c>&lt;|"|&gt;</c>), Gemma 2/3 (<c>&lt;start_of_turn&gt;</c>), ChatML
    /// (<c>&lt;|im_start|&gt;</c>), Llama 3 (<c>&lt;|eot_id|&gt;</c>), DeepSeek'in tam genişlikli çubuklu belirteçleri
    /// (<c>&lt;｜User｜&gt;</c>) ve Llama 2 (<c>[INST]</c>, <c>&lt;&lt;SYS&gt;&gt;</c>). Kullanıcı ya da doküman metninde
    /// bunlar sahte bir sistem ya da model sırası açabilir.
    /// </summary>
    /// <remarks>
    /// Gemma 4 kalıpları, çalışan llama.cpp sunucusunun <c>/props</c> uç noktasındaki sohbet şablonundan alındı; ilk sürüm
    /// yalnızca simetrik <c>&lt;|…|&gt;</c> biçimini tanıyordu ve tercih edilen modelin sıra belirteçlerini kaçırıyordu.
    /// Belirteç içi boşluk içeremez ve en fazla 40 karakterdir; böylece "&lt;750" ya da "A|B" gibi gündelik yazımlar
    /// eşleşmez.
    /// </remarks>
    [GeneratedRegex(@"<\|[^<>|\s]{1,40}\|?>|<[a-z_]{1,40}\|>|<｜[^<>｜\s]{1,40}｜>|</?(start|end)_of_turn>|\[/?INST\]|<</?SYS>>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChatTemplateToken();

    /// <summary>
    /// Büyük harfle yazılmış "DAN modu / mode" (jailbreak). Ham metinde, büyük/küçük harf duyarlı aranır ve önünde harf,
    /// rakam ya da kesme işareti bulunamaz: "Alexa'dan modu" ve "Hub'dan modu" gibi ayrılma hâli ekleri eşleşmez.
    /// </summary>
    [GeneratedRegex(@"(?<![\p{L}\p{N}'’`])DAN\s+mod\w*", RegexOptions.CultureInvariant)]
    private static partial Regex DanMode();

    /// <summary>
    /// Talimatın hedefi: bir niteleyiciyle (önceki, yukarıdaki, tüm, verilen, sistem…) birlikte geçen talimat / kural /
    /// yönerge / komut / prompt. Niteleyici şartı, "kurulum talimatları" gibi masum kullanımları dışarıda bırakır.
    /// </summary>
    [GeneratedRegex(@"\b(onceki|yukaridaki|ustteki|tum|butun|verilen|mevcut|sistem) (talimat|kural|yonerge|komut|prompt)\w*", RegexOptions.CultureInvariant)]
    private static partial Regex InstructionTarget();

    /// <summary>
    /// Talimatı geçersiz kılan emir kipleri: yok say, unut, görmezden gel, dikkate alma, devre dışı bırak (tekil ve çoğul).
    /// Sözcük sınırı şarttır: "unuttum" geçmiş zamandır ve eşleşmez.
    /// </summary>
    [GeneratedRegex(@"\b(yok ?say(in|iniz)?|unut(un|unuz)?|gormezden gel(in|iniz)?|dikkate alma(yin|yiniz)?|devre disi birak(in)?)\b", RegexOptions.CultureInvariant)]
    private static partial Regex DismissalVerb();

    /// <summary>
    /// Bir destek sorusunda geçmesi için hiçbir neden olmayan terimler: sistem prompt'u ve talimatları, gizli talimat,
    /// jailbreak, "do anything now".
    /// </summary>
    [GeneratedRegex(@"\b(sistem prompt|system prompt|sistem talimat|sistem yonerge|gizli talimat|jailbreak|do anything now)", RegexOptions.CultureInvariant)]
    private static partial Regex SuspiciousTerm();

    /// <summary>
    /// Geliştirici modu (Türkçe ya da İngilizce). Tek başına şüpheli değildir, telefonların ve uygulamaların gerçek bir
    /// ayarıdır; <see cref="RuleWord"/> ile birlikte geçtiğinde ("geliştirici moduna geç ve kuralları kapat") aranır.
    /// </summary>
    [GeneratedRegex(@"\b(gelistirici mod|developer mode)\w*", RegexOptions.CultureInvariant)]
    private static partial Regex DeveloperMode();

    /// <summary>
    /// Modelin sınırlarını anlatan sözcükler: kural, talimat, kısıtlama, sınırlama, filtre (Türkçe ve İngilizce).
    /// Geliştirici modu kalıbının ikinci şartıdır.
    /// </summary>
    [GeneratedRegex(@"\b(kural|talimat|kisitlama|sinirlama|filtre|rules|restrictions|filters|instructions|guardrails)\w*", RegexOptions.CultureInvariant)]
    private static partial Regex RuleWord();

    /// <summary>
    /// İngilizce talimat geçersiz kılma: "ignore / disregard / forget / override / bypass" ile en fazla dört sözcük sonra
    /// gelen "instructions / rules / prompt / guidelines".
    /// </summary>
    [GeneratedRegex(@"\b(ignore|disregard|forget|override|bypass)\b( \w+){0,4} (instruction|instructions|rules|prompt|prompts|guidelines|directions)\b", RegexOptions.CultureInvariant)]
    private static partial Regex EnglishOverride();

    /// <summary>İngilizce rol değiştirme kalıpları: "you are now", "you are no longer", "pretend to be / you are".</summary>
    [GeneratedRegex(@"\b(you are now|you are no longer|pretend to be|pretend you are|from now on you are)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RoleSwitch();

    /// <summary>
    /// Türkçe rol değiştirme: "artık" ya da "bundan sonra"dan sonra en fazla beş sözcük içinde, ikinci tekil ya da çoğul
    /// kişi ekiyle bir rol adı ("asistansın", "yapay zekasın", "botsun", "modelsiniz"). Ek şartı, "Siz artık bot mu
    /// kullanıyorsunuz?" gibi soruları dışarıda bırakır.
    /// </summary>
    [GeneratedRegex(@"\b(artik|bundan sonra)\b( \w+){0,5} (asistan|bot|model|yapay zeka)s(in|un|iniz|unuz)\b", RegexOptions.CultureInvariant)]
    private static partial Regex TurkishRoleSwitch();
}
