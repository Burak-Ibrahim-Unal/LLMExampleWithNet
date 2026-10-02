namespace Knowledge.Infrastructure.Ingestion;

/// <summary>
/// Bilgi tabanı kaynağının yapılandırması: <c>KnowledgeBase</c> bölümünden (ortam değişkeni olarak
/// <c>KnowledgeBase__Path</c>) bağlanır ve <see cref="MarkdownKnowledgeSource"/> tarafından okunur.
/// </summary>
public sealed class KnowledgeBaseOptions
{
    /// <summary>Bu seçeneklerin bağlandığı yapılandırma bölümünün adı.</summary>
    public const string SectionName = "KnowledgeBase";

    /// <summary>
    /// Markdown dokümanlarının bulunduğu klasör (varsayılan <c>knowledge-base</c>). Göreli bir yol, çalışma dizininden
    /// ve uygulama dizininden başlayarak yukarı doğru aranır; böylece aynı varsayılan değer depo kökünden, proje
    /// klasöründen, <c>bin/</c> altından ve testlerden çalışırken de bulunur. Mutlak bir yol olduğu gibi kullanılır.
    /// </summary>
    public string Path { get; set; } = "knowledge-base";
}
