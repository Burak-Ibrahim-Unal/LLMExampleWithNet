using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;

namespace SupportAssistant.UnitTests.TestDoubles;

/// <summary>
/// Markdown bilgi tabanı okuyucusunun (<see cref="IKnowledgeBaseSource"/>) yerine geçen stub. Testler dokümanları dosya
/// sistemi olmadan doğrudan bellekte tanımlar ya da bir okuma hatasını taklit eder.
/// </summary>
/// <remarks>
/// Ingestion testleri böylece Markdown ayrıştırmasından bağımsız kalır (ayrıştırıcı kendi testlerinde sınanır) ve
/// yalnızca veritabanıyla uzlaştırma mantığına odaklanır.
/// </remarks>
internal sealed class StubKnowledgeBaseSource : IKnowledgeBaseSource
{
    /// <summary>
    /// <see cref="LoadAsync"/>'in döndüreceği dokümanlar. Testler bunu ingestion'lar arasında değiştirerek dosyası
    /// düzenlenmiş (yeni içerik özeti), silinmiş ya da çakışan kimlikli doküman senaryolarını kurar.
    /// </summary>
    public IReadOnlyList<SourceDocument> Documents { get; set; } = [];

    /// <summary>
    /// Doluysa <see cref="LoadAsync"/> bu istisnayla başarısız olur. Bozuk front matter (<c>KnowledgeBaseFormatException</c>)
    /// ve okunamayan dosya (<c>IOException</c>, <c>UnauthorizedAccessException</c>) durumlarının 422'ye eşlendiğini sınamak
    /// için kullanılır.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary><see cref="Failure"/> doluysa hata veren bir görev, değilse <see cref="Documents"/> listesini döndürür.</summary>
    public Task<IReadOnlyList<SourceDocument>> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Failure is not null ? Task.FromException<IReadOnlyList<SourceDocument>>(Failure) : Task.FromResult(Documents);
    }

    /// <summary>
    /// Testlerin önemsediği alanları (kaynak kimliği, içerik özeti, bölümler) alan bir doküman fabrikası; diğer alanlar
    /// sabittir. Aile anahtarı kimlikle aynıdır, yani her doküman kendi ailesinin tek sürümüdür.
    /// </summary>
    /// <remarks>
    /// İçerik özeti, içerikten hesaplanmak yerine parametre olarak verilir: uzlaştırma "doküman değişti mi" kararını
    /// yalnızca bu özete bakarak verdiğinden testler bu kararı açıkça kontrol eder.
    /// </remarks>
    public static SourceDocument Document(string sourceId, string contentHash, params SourceSection[] sections)
    {
        return new SourceDocument(
            sourceId,
            DocumentKey: sourceId,
            Title: $"Başlık {sourceId}",
            Version: "1.0",
            EffectiveDate: new DateOnly(2025, 1, 1),
            DocumentStatus.Active,
            DocumentCategory.Policy,
            Supersedes: null,
            contentHash,
            sections);
    }
}
