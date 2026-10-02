using System.Text.RegularExpressions;
using Knowledge.Application.Text;

namespace Knowledge.Application.Security;

/// <summary>
/// Bir metinde dil modelinin talimatlarını değiştirmeye yönelik kalıpları (prompt injection) arar: talimatları yok
/// saydırma, sistem prompt'unu açıklatma, rol değiştirme, jailbreak terimleri ve sohbet şablonu belirteçleri. Soru
/// tarafında modele gitmeden önce, ingest tarafında da bilgi tabanı dokümanları için kullanılır.
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

        // Sohbet şablonu belirteçleri ve satır başı rol işaretleri ham metinde aranır: normalleştirme onları oluşturan
        // noktalamayı (<, |, :) siler.
        if (ChatTemplateToken().IsMatch(text))
        {
            return "chat-template-token";
        }

        if (RoleMarker().IsMatch(text))
        {
            return "role-marker";
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

        if (EnglishOverride().IsMatch(normalized))
        {
            return "override-instructions-en";
        }

        if (RoleSwitch().IsMatch(normalized))
        {
            return "role-switch";
        }

        return null;
    }

    /// <summary>
    /// Sohbet şablonlarının özel belirteçleri: Gemma (<c>&lt;start_of_turn&gt;</c>), ChatML (<c>&lt;|im_start|&gt;</c>),
    /// Llama 3 (<c>&lt;|eot_id|&gt;</c>) ve Llama 2 (<c>[INST]</c>, <c>&lt;&lt;SYS&gt;&gt;</c>). Kullanıcı metninde bunlar
    /// sahte bir sistem ya da model sırası açabilir.
    /// </summary>
    [GeneratedRegex(@"<\|[a-z_]+\|>|</?(start|end)_of_turn>|\[/?INST\]|<</?SYS>>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChatTemplateToken();

    /// <summary>Satır başında bir rol işareti (<c>system:</c>, <c>assistant:</c>, <c>sistem:</c>): metinde sahte bir konuşma sırası açma girişimi.</summary>
    [GeneratedRegex(@"^\s*(system|assistant|sistem|asistan)\s*:", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex RoleMarker();

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
    /// jailbreak, geliştirici modu, "do anything now".
    /// </summary>
    [GeneratedRegex(@"\b(sistem prompt|system prompt|sistem talimat|sistem yonerge|gizli talimat|jailbreak|developer mode|gelistirici mod|dan modu|do anything now)", RegexOptions.CultureInvariant)]
    private static partial Regex SuspiciousTerm();

    /// <summary>
    /// İngilizce talimat geçersiz kılma: "ignore / disregard / forget / override / bypass" ile en fazla dört sözcük sonra
    /// gelen "instructions / rules / prompt / guidelines".
    /// </summary>
    [GeneratedRegex(@"\b(ignore|disregard|forget|override|bypass)\b( \w+){0,4} (instruction|instructions|rules|prompt|prompts|guidelines|directions)\b", RegexOptions.CultureInvariant)]
    private static partial Regex EnglishOverride();

    /// <summary>İngilizce rol değiştirme kalıpları: "you are now", "you are no longer", "pretend to be / you are".</summary>
    [GeneratedRegex(@"\b(you are now|you are no longer|pretend to be|pretend you are|from now on you are)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RoleSwitch();
}
