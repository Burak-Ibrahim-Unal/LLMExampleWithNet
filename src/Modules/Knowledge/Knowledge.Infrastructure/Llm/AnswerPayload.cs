using System.ComponentModel;

namespace Knowledge.Infrastructure.Llm;

// Structured output contract sent to the model as a JSON schema. No property is nullable: llama.cpp turns the
// schema into a grammar, and plain types (empty string / empty list instead of null) convert most reliably.

public sealed class AnswerPayload
{
    [Description("Kaynaklar soruyu yanıtlamaya yetiyorsa true, yetmiyorsa false.")]
    public bool Answerable { get; set; }

    [Description("Temsilcinin müşteriye iletebileceği kısa Türkçe yanıt. Kaynak kimliği içermez. Yanıtlanamıyorsa boş.")]
    public string Answer { get; set; } = string.Empty;

    [Description("Yanıttaki her bilgiyi destekleyen kaynaklar.")]
    public List<CitationPayload> Citations { get; set; } = [];

    [Description("Kaynaklarda bulunmayan, sorunun yanıtlanamayan kısmı; yoksa boş.")]
    public string MissingInformation { get; set; } = string.Empty;

    [Description("Kaynaklar arasında tespit edilen çelişkiler; yoksa boş liste.")]
    public List<ConflictPayload> Conflicts { get; set; } = [];
}

public sealed class CitationPayload
{
    [Description("Kaynak kimliği, örneğin C1.")]
    public string ChunkId { get; set; } = string.Empty;

    [Description("Kaynaktan birebir kopyalanmış kısa alıntı.")]
    public string Quote { get; set; } = string.Empty;
}

public sealed class ConflictPayload
{
    [Description("Çelişkinin konusu.")]
    public string Topic { get; set; } = string.Empty;

    [Description("Geçerli kabul edilen kaynağın kimliği.")]
    public string ChosenChunkId { get; set; } = string.Empty;

    [Description("Elenen kaynakların kimlikleri.")]
    public List<string> RejectedChunkIds { get; set; } = [];

    [Description("Seçim gerekçesi.")]
    public string Reason { get; set; } = string.Empty;
}
