using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Knowledge.Infrastructure.Llm;

/// <summary>
/// Dil modeline giden metinlerin (<c>Llm/Prompts/answer-prompt.yaml</c>) bellekteki karşılığı: sistem prompt'u, öncelik
/// kuralı, yeniden deneme talimatı, kullanıcı mesajı kalıpları, düzeltme bloğunun parçaları ve JSON şemasının alan
/// açıklamaları. Dosya derlemeye gömülü kaynak olarak girer ve <see cref="Load"/> ile bir kez okunur.
/// </summary>
/// <remarks>
/// <para>
/// Prompt'un koddan ayrı bir dosyada durması, metnin C# değişikliği olmadan gözden geçirilmesini ve düzenlenmesini
/// sağlar; kod yalnızca parçaları birleştirir (<see cref="AnswerPrompt"/>). Metin yine de davranışın parçasıdır: dosya
/// değiştiğinde değerlendirme yeniden koşulur.
/// </para>
/// <para>
/// Dosya yüklenirken doğrulanır: her metin dolu olmalı, kalıplar yalnızca beklenen yer tutucuları taşımalı ve şema
/// bölümü yanıt şemasının (<see cref="AnswerPayload"/>, <see cref="CitationPayload"/>, <see cref="ConflictPayload"/>)
/// her alanını açıklamalıdır. Bilinmeyen bir anahtar da hatadır. Böylece kalıptan silinen bir <c>{question}</c> sorunun
/// modele hiç gitmemesine, yanlış yazılmış bir anahtar da sessizce boş bir metne yol açmaz; hata ilk kullanımda ve
/// birim testlerinde görünür.
/// </para>
/// </remarks>
internal sealed class AnswerPromptTexts
{
    /// <summary>Gömülü kaynağın adı; proje dosyasında <c>LogicalName</c> ile sabitlenir.</summary>
    private const string ResourceName = "Knowledge.Infrastructure.Llm.Prompts.answer-prompt.yaml";

    /// <summary>Sistem prompt'unun kalıbı; tek yer tutucusu <c>{precedenceRule}</c>'dur.</summary>
    public string System { get; set; } = string.Empty;

    /// <summary>Sistem prompt'unun 5. kuralı olan kaynak önceliği kuralı.</summary>
    public string PrecedenceRule { get; set; } = string.Empty;

    /// <summary>Şemaya uymayan çıktıdan sonra gönderilen düzeltici talimat.</summary>
    public string RetryInstruction { get; set; } = string.Empty;

    /// <summary>Kullanıcı mesajının kalıpları.</summary>
    public UserMessageTexts UserMessage { get; set; } = new();

    /// <summary>Düzeltme bloğunun parçaları.</summary>
    public CorrectionTexts Correction { get; set; } = new();

    /// <summary>JSON şemasının alan açıklamaları: şema türünün adı → JSON alan adı → açıklama.</summary>
    public Dictionary<string, Dictionary<string, string>> Schema { get; set; } = [];

    /// <summary>
    /// Gömülü dosyayı okur, doğrular ve döndürür. Dosya bulunamaz, ayrıştırılamaz ya da doğrulamadan geçemezse
    /// <see cref="InvalidOperationException"/> fırlatır; ileti bütün sorunları birlikte listeler.
    /// </summary>
    /// <remarks>
    /// YAML ayrıştırıcısı bilinmeyen anahtarları reddeder (anahtar yazım hatası sessiz kalmaz) ve blok metinlerdeki satır
    /// sonlarını <c>\n</c>'e normalleştirir; dosya Windows'ta CRLF ile çekilmiş olsa bile prompt her işletim sisteminde
    /// aynıdır.
    /// </remarks>
    public static AnswerPromptTexts Load()
    {
        using var stream = typeof(AnswerPromptTexts).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The prompt resource '{ResourceName}' is missing from the assembly.");
        using var reader = new StreamReader(stream);

        var texts = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build()
            .Deserialize<AnswerPromptTexts>(reader) ?? new AnswerPromptTexts();

        var problems = texts.Problems().ToList();

        return problems.Count == 0
            ? texts
            : throw new InvalidOperationException($"The prompt file '{ResourceName}' is invalid: {string.Join("; ", problems)}");
    }

    /// <summary>
    /// Dosyadaki sorunları sıralar: eksik metinler, beklenmeyen ya da eksik yer tutucular ve açıklaması olmayan ya da
    /// şemada bulunmayan alanlar. Birim testleri bozuk bir dosyanın her sorununun adlandırıldığını bununla doğrular.
    /// </summary>
    internal IEnumerable<string> Problems()
    {
        var templates = new (string Key, string Text, string[] Placeholders)[]
        {
            ("system", System, ["precedenceRule"]),
            ("precedenceRule", PrecedenceRule, []),
            ("retryInstruction", RetryInstruction, []),
            ("userMessage.sources", UserMessage.Sources, []),
            ("userMessage.sourceHeader", UserMessage.SourceHeader, ["label", "title", "version", "effectiveDate", "category"]),
            ("userMessage.section", UserMessage.Section, ["section"]),
            ("userMessage.question", UserMessage.Question, ["question"]),
            ("correction.header", Correction.Header, []),
            ("correction.citationsNotMapped", Correction.CitationsNotMapped, []),
            ("correction.unverifiedQuotes", Correction.UnverifiedQuotes, []),
            ("correction.quote", Correction.Quote, ["quote"]),
            ("correction.invalidConflictReferences", Correction.InvalidConflictReferences, []),
            ("correction.winnerNotCited", Correction.WinnerNotCited, []),
            ("correction.regenerate", Correction.Regenerate, []),
            ("correction.copyQuotes", Correction.CopyQuotes, []),
            ("correction.refuseIfInsufficient", Correction.RefuseIfInsufficient, [])
        };

        foreach (var (key, text, placeholders) in templates)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield return $"'{key}' is empty";
            }
            else if (!PromptTemplate.Placeholders(text).SetEquals(placeholders))
            {
                yield return $"'{key}' must use exactly these placeholders: {string.Join(", ", placeholders.Select(name => "{" + name + "}"))}";
            }
        }

        foreach (var type in new[] { typeof(AnswerPayload), typeof(CitationPayload), typeof(ConflictPayload) })
        {
            var fields = type.GetProperties().Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)).ToHashSet(StringComparer.Ordinal);
            var described = Schema.GetValueOrDefault(type.Name) ?? [];

            foreach (var field in fields.Where(field => string.IsNullOrWhiteSpace(described.GetValueOrDefault(field))))
            {
                yield return $"'schema.{type.Name}.{field}' is missing";
            }

            foreach (var field in described.Keys.Where(field => !fields.Contains(field)))
            {
                yield return $"'schema.{type.Name}.{field}' is not a field of the schema";
            }
        }

        foreach (var type in Schema.Keys.Where(name => name is not (nameof(AnswerPayload) or nameof(CitationPayload) or nameof(ConflictPayload))))
        {
            yield return $"'schema.{type}' is not a schema type";
        }
    }
}

/// <summary>Kullanıcı mesajının kalıpları (<c>userMessage</c> bölümü).</summary>
internal sealed class UserMessageTexts
{
    /// <summary>Kaynak listesinin başlığı (<c>KAYNAKLAR:</c>).</summary>
    public string Sources { get; set; } = string.Empty;

    /// <summary>Bir kaynağın başlık satırı: etiket, doküman başlığı, sürüm, yürürlük tarihi ve tür.</summary>
    public string SourceHeader { get; set; } = string.Empty;

    /// <summary>Bir kaynağın bölüm satırı.</summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>Mesajın son satırı olan soru.</summary>
    public string Question { get; set; } = string.Empty;
}

/// <summary>Düzeltme bloğunun parçaları (<c>correction</c> bölümü); kod, geri bildirime göre bunları birleştirir.</summary>
internal sealed class CorrectionTexts
{
    /// <summary>Bloğun ilk cümlesi (<c>DÜZELTME:</c> işaretiyle başlar).</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>Atıfların verilen kaynak kimliklerinden hiçbirine dayanmadığını söyleyen cümle.</summary>
    public string CitationsNotMapped { get; set; } = string.Empty;

    /// <summary>Doğrulanamayan alıntı listesinin giriş cümlesi.</summary>
    public string UnverifiedQuotes { get; set; } = string.Empty;

    /// <summary>Doğrulanamayan tek bir alıntının liste satırı.</summary>
    public string Quote { get; set; } = string.Empty;

    /// <summary>Çelişki kayıtlarındaki kimliklerin verilen kaynaklarla eşleşmediğini söyleyen cümleler.</summary>
    public string InvalidConflictReferences { get; set; } = string.Empty;

    /// <summary>Bildirilen çelişkinin geçerli kaynağına atıf yapılmadığını söyleyen cümleler.</summary>
    public string WinnerNotCited { get; set; } = string.Empty;

    /// <summary>Yanıtın yeniden üretilmesini isteyen cümlenin başı.</summary>
    public string Regenerate { get; set; } = string.Empty;

    /// <summary>Atıflar reddedildiyse yeniden üretme cümlesine eklenen, alıntıların birebir kopyalanmasını isteyen kısım.</summary>
    public string CopyQuotes { get; set; } = string.Empty;

    /// <summary>Kaynaklar yetmiyorsa açık ret yolunu hatırlatan son cümle.</summary>
    public string RefuseIfInsufficient { get; set; } = string.Empty;
}
