using Knowledge.Application.Answering;
using Knowledge.Application.Contracts;
using Knowledge.Application.Text;
using Shared.Application.Common;

namespace SupportAssistant.Eval;

/// <summary>
/// Tek bir yanıtı beklentisine karşı deterministik olarak denetler; puanlamada hiçbir dil modeli kullanılmaz.
/// </summary>
/// <remarks>
/// LLM-as-judge yerine kural tabanlı kontroller seçildi: sonuç her koşuda aynıdır, bir sorunun neden kaldığı açıkça
/// görülür ve sistemi oluşturan küçük modelin kendi çıktısını yargılaması gibi zayıf bir ölçüte dayanılmaz.
/// </remarks>
public static class EvalChecks
{
    /// <summary>
    /// Bir yanıt için kontrol listesini üretir: yanıtlanabilirlik; cevapsız sorularda ret sözleşmesi; yanıtlanan sorularda
    /// beklenen kaynak(lar), yasak kaynaklar, beklenen bölüm, alıntıların doğrulanmış olması, sayıların kaynağa dayanması,
    /// zorunlu ifade grupları, kritik kararların koşul ifadeleri, yasak ifadeler, elenmesi gereken eski sürümler ve
    /// beklenen çelişki kaydı. Her kontrol, raporda gösterilecek Türkçe bir ad ve beklenen/gerçek özetiyle döner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kaynak kontrolleri yanıt metnine değil <c>sources</c> alanındaki doküman kimliklerine bakar; "doğru cevap" ile
    /// "doğru kaynak" birlikte denetlenir. Elenen sürüm kontrolü, çelişkili sorularda eski sürümün yalnızca
    /// kullanılmamasını değil, elendiğinin <c>versionResolution.discarded</c> içinde açıkça raporlanmasını da şart koşar.
    /// </para>
    /// <para>
    /// İfade kontrolleri anahtar kelimelere dayandığı için yanlış bir kararı ya da uydurma bir sayıyı kendi başına
    /// yakalayamaz ("doğru kaynak + yanlış karar"). Bu yüzden yanıttaki her sayı ayrıca atıf yapılan dokümanların
    /// metniyle karşılaştırılır. Sayının kaynakta geçmesi doğru kullanıldığını da kanıtlamaz ("750 TL altındaki
    /// siparişlerde kargo ücretsizdir"); kritik kararlarda koşulun yönü ayrı bir "koşul" kontrolüyle, ters yazımları da
    /// yasak ifadelerle denetlenir. Bu denetim ifade tabanlı olduğu için kısmidir; kontroller anlamsal doğruluğun kanıtı
    /// değildir ve değerlendirme raporundaki gerçek yanıtlar ayrıca elle okunur.
    /// </para>
    /// </remarks>
    /// <param name="expect">Sorunun beklentileri.</param>
    /// <param name="answer">API'nin yanıtı.</param>
    /// <param name="question">Soru metni; sorunun kendisinde geçen sayılar (ör. "749 TL'lik sipariş") yanıtta da geçebilir.</param>
    /// <param name="documentTexts">Doküman kimliğinden doküman metnine (başlık, sürüm, tarih, bölümler); sayı kontrolü için.</param>
    public static IReadOnlyList<CheckResult> Evaluate(EvalExpectation expect, AnswerDto answer, string question, IReadOnlyDictionary<string, string> documentTexts)
    {
        var checks = new List<CheckResult>
        {
            new("yanıtlanabilirlik", answer.Answerable == expect.Answerable,
                $"beklenen: {(expect.Answerable ? "yanıt" : "bilgi yok")}, gerçek: {(answer.Answerable ? "yanıt" : $"bilgi yok ({answer.RefusalReason})")}")
        };

        // Bir ret yalnızca reddin kendisiyle değerlendirilir; reddedilmiş (ya da yanıtlanmaması gerekirken yanıtlanmış)
        // bir sorunun içerik kontrolleri hiçbir şey söylemez. Beklenen bir ret ise API'nin ret sözleşmesine de uymalıdır.
        if (!expect.Answerable)
        {
            if (!answer.Answerable)
            {
                checks.Add(RefusalContract(answer));
            }

            return checks;
        }

        if (!answer.Answerable)
        {
            return checks;
        }

        // Aynı dokümanın birden çok bölümüne atıf yapılabilir; kaynak kontrolleri doküman düzeyinde olduğu için tekilleştirilir.
        var cited = answer.Sources.Select(source => source.DocumentId).Distinct(StringComparer.Ordinal).ToList();

        if (expect.SourcesAnyOf is { Count: > 0 } anyOf)
        {
            checks.Add(new CheckResult("kaynak", anyOf.Any(cited.Contains), $"beklenen: {string.Join(" | ", anyOf)}; atıf: {Join(cited)}"));
        }

        if (expect.SourcesAllOf is { Count: > 0 } allOf)
        {
            checks.Add(new CheckResult("kaynaklar", allOf.All(cited.Contains), $"beklenen: {string.Join(" + ", allOf)}; atıf: {Join(cited)}"));
        }

        if (expect.ForbiddenSources is { Count: > 0 } forbidden)
        {
            var used = forbidden.Where(cited.Contains).ToList();
            checks.Add(new CheckResult("yasak kaynak yok", used.Count == 0, used.Count == 0 ? "yok" : $"atıf yapılmış: {Join(used)}"));
        }

        if (expect.SectionsAnyOf is { Count: > 0 } sections)
        {
            var citedSections = answer.Sources.Select(source => source.Section).Distinct(StringComparer.Ordinal).ToList();
            checks.Add(new CheckResult(
                "bölüm",
                sections.Any(expected => citedSections.Any(section => ContainsPhrase(section, expected))),
                $"beklenen: {string.Join(" | ", sections)}; atıf: {Join(citedSections)}"));
        }

        var unverified = answer.Sources.Where(source => !source.QuoteVerified).Select(source => source.DocumentId).ToList();
        checks.Add(new CheckResult(
            "alıntı doğrulandı",
            answer.Sources.Count > 0 && unverified.Count == 0,
            answer.Sources.Count == 0 ? "kaynak yok" : $"doğrulanmamış: {Join(unverified)}"));

        // Sayı yoksa kontrol üretilmez: denetlenecek bir şey olmayan "geçti" satırları raporu yalnızca kalabalıklaştırırdı.
        var numbers = Numbers(answer.Answer);

        if (numbers.Count > 0)
        {
            var grounded = Numbers(question).ToHashSet(StringComparer.Ordinal);

            foreach (var documentId in cited)
            {
                if (documentTexts.TryGetValue(documentId, out var text))
                {
                    grounded.UnionWith(Numbers(text));
                }
            }

            var unsupported = numbers.Where(number => !grounded.Contains(number)).Distinct(StringComparer.Ordinal).ToList();
            checks.Add(new CheckResult("sayılar kaynakta", unsupported.Count == 0, $"atıf yapılan dokümanlarda olmayan: {Join(unsupported)}"));
        }

        foreach (var group in expect.MustContain ?? [])
        {
            checks.Add(new CheckResult("içerik", group.Any(phrase => ContainsPhrase(answer.Answer, phrase)), $"'{string.Join("' | '", group)}'"));
        }

        // Koşul grupları içerikle aynı biçimde değerlendirilir ama ayrı adla raporlanır: sayının yanıtta geçmesi ("750")
        // ile koşulun doğru yönde söylenmesi ("750 TL ve üzeri") ayrı sorulardır.
        foreach (var group in expect.Conditions ?? [])
        {
            checks.Add(new CheckResult("koşul", group.Any(phrase => ContainsPhrase(answer.Answer, phrase)), $"'{string.Join("' | '", group)}'"));
        }

        foreach (var phrase in expect.MustNotContain ?? [])
        {
            checks.Add(new CheckResult("yasak ifade", !ContainsPhrase(answer.Answer, phrase), $"'{phrase}'"));
        }

        if (expect.DiscardedVersions is { Count: > 0 } discarded)
        {
            var reported = answer.VersionResolution.Discarded.Select(version => version.DocumentId).ToList();
            checks.Add(new CheckResult("eski sürüm elendi", discarded.All(reported.Contains), $"beklenen: {Join(discarded)}; raporlanan: {Join(reported)}"));
        }

        if (expect.ExpectConflict is { } conflict)
        {
            var found = answer.Conflicts.Any(reported =>
                reported.RuleSatisfied
                && reported.Chosen.DocumentId == conflict.Chosen
                && conflict.Rejected.All(rejected => reported.Rejected.Any(source => source.DocumentId == rejected)));
            var summary = answer.Conflicts.Select(reported =>
                $"{reported.Chosen.DocumentId} > {string.Join("+", reported.Rejected.Select(source => source.DocumentId))} (kurala uygun: {(reported.RuleSatisfied ? "evet" : "hayır")})").ToList();
            checks.Add(new CheckResult(
                "çelişki kaydı",
                found,
                $"beklenen: {conflict.Chosen} > {string.Join("+", conflict.Rejected)}; raporlanan: {Join(summary)}"));
        }

        return checks;
    }

    /// <summary>
    /// Bir reddin API sözleşmesine uyup uymadığını denetler: yanıt metni, ret gerekçesine ait sabit mesajdır (genelde
    /// "yeterli bilgi bulunamadı"; prompt injection ve çıktı koruması retlerinde onlara özel mesaj), kaynak listesi boştur
    /// ve hangi kapıda durulduğunu söyleyen bir <c>refusalReason</c> vardır.
    /// </summary>
    /// <remarks>
    /// Yalnızca <c>answerable=false</c>'a bakmak, kaynak gösteren ya da modelin kendi metnini döndüren bir "ret"i de
    /// başarılı sayardı. İstemciler bu üç alana dayanır; sözleşmenin uçtan uca korunduğu burada görülür. Mesajın gerekçeyle
    /// eşleşmesi de denetlenir: sorun soruda ya da model çıktısında olduğunda "bilgi yok" demek kullanıcıyı yanıltırdı.
    /// </remarks>
    private static CheckResult RefusalContract(AnswerDto answer)
    {
        var problems = new List<string>();
        var expectedMessage = answer.RefusalReason switch
        {
            RefusalReasons.PromptInjectionSuspected => Messages.Knowledge.PromptInjectionRefused,
            RefusalReasons.UnsafeOutput => Messages.Knowledge.UnsafeOutputRefused,
            _ => Messages.Knowledge.NotEnoughInformation
        };

        if (answer.Answer != expectedMessage)
        {
            problems.Add("ret metni gerekçenin sabit mesajı değil");
        }

        if (answer.Sources.Count > 0)
        {
            problems.Add($"kaynak listelenmiş: {Join(answer.Sources.Select(source => source.DocumentId).ToList())}");
        }

        if (string.IsNullOrWhiteSpace(answer.RefusalReason))
        {
            problems.Add("refusalReason boş");
        }

        return new CheckResult("ret sözleşmesi", problems.Count == 0, Join(problems));
    }

    /// <summary>
    /// Metindeki sayıları, normalleştirilmiş sözcüklerden yalnızca rakamlardan oluşanlar olarak çıkarır; baştaki sıfırlar
    /// atılır ("09:00" → "9", "0", "49,90" → "49", "90").
    /// </summary>
    /// <remarks>
    /// Aynı normalleştirme hem yanıta hem doküman metnine uygulandığı için ondalık ayırıcı, saat ve aralık yazımı
    /// ("2.4" / "2,4", "1–2") iki tarafta da aynı parçalara ayrılır. Baştaki sıfırların atılması "09:00" ile "9:00"
    /// yazımını eşitler.
    /// </remarks>
    private static IReadOnlyList<string> Numbers(string text) =>
        TurkishTextNormalizer.Normalize(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.All(char.IsAsciiDigit))
            .Select(token => token.TrimStart('0') is { Length: > 0 } trimmed ? trimmed : "0")
            .ToList();

    /// <summary>
    /// Büyük/küçük harf, noktalama ve Türkçe karakterden bağımsız, bir kelime başında başlaması gereken ifade eşleşmesi:
    /// "ücretsiz", "ücretsizdir" ile eşleşir (Türkçe ekler); "30 gün" ise "300 gün" ile eşleşmez.
    /// </summary>
    /// <remarks>
    /// Metin ve ifade, aramada kullanılan <see cref="TurkishTextNormalizer"/> ile aynı biçime indirilir (tr-TR küçük harf,
    /// ç/ğ/ı/ö/ş/ü katlama, harf ve rakam dışındaki her dizi tek boşluk); modelin "kaç gün" ya da "kac gun" yazması fark
    /// etmez. Başa eklenen boşluk eşleşmenin bir kelimenin başında başlamasını zorunlu kılar; sonu ise bilinçli olarak açık
    /// bırakılır ki eklemeli Türkçede "karşıla" → "karşılar" / "karşılamaktadır" gibi çekimler yakalansın. Bunun bedeli,
    /// yalnızca sayıdan oluşan bir ifadenin aynı rakamlarla başlayan daha uzun bir sayıyla da eşleşmesidir ("750" →
    /// "7500"). Normalleştirme sonrası boş kalan ifade hiçbir zaman eşleşmez.
    /// </remarks>
    public static bool ContainsPhrase(string text, string phrase)
    {
        var normalizedPhrase = TurkishTextNormalizer.Normalize(phrase);

        return normalizedPhrase.Length > 0
            && (" " + TurkishTextNormalizer.Normalize(text)).Contains(" " + normalizedPhrase, StringComparison.Ordinal);
    }

    /// <summary>
    /// Aramanın beklenen kaynağı (ya da kaynakları) bulup bulmadığını söyler; beklenen kaynağı olmayan sorular (cevapsız
    /// sorular) için <c>null</c> döner.
    /// </summary>
    /// <remarks>
    /// <c>SourcesAllOf</c> tanımlıysa tüm dokümanların sonuçlarda olması gerekir (iki dokümana yayılan sorular); yoksa
    /// <c>SourcesAnyOf</c>'tan biri yeterlidir. Ölçüm yanıttan bağımsızdır: bir soru kaldığında hatanın erişimde mi
    /// (kaynak hiç bulunamadı) yoksa kapılarda ya da üretimde mi olduğunu ayırt etmeyi sağlar.
    /// </remarks>
    public static bool? RetrievalHit(EvalExpectation expect, IReadOnlyCollection<string> retrievedDocumentIds)
    {
        if (expect.SourcesAllOf is { Count: > 0 } allOf)
        {
            return allOf.All(retrievedDocumentIds.Contains);
        }

        if (expect.SourcesAnyOf is { Count: > 0 } anyOf)
        {
            return anyOf.Any(retrievedDocumentIds.Contains);
        }

        return null;
    }

    /// <summary>
    /// Kontrol ayrıntısı için değerleri virgülle birleştirir. Boş liste "—" olarak yazılır; ayrıntı metninde "hiçbiri"
    /// (ör. hiç atıf yapılmamış olması) eksik ya da kaybolmuş bir değer gibi görünmez.
    /// </summary>
    private static string Join(IReadOnlyCollection<string> values) => values.Count == 0 ? "—" : string.Join(", ", values);
}
