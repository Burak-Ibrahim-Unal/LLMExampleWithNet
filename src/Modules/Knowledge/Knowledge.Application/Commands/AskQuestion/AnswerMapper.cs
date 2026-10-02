using System.Diagnostics;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.Contracts;

namespace Knowledge.Application.Commands.AskQuestion;

/// <summary>
/// Soru-cevap hattının iç sonuçlarını yanıt sözleşmesinin alanlarına çevirir: sürüm kararları
/// (<c>versionResolution</c>), kaynaklar (<c>sources</c>) ve tanılama (<c>diagnostics</c>).
/// </summary>
/// <remarks>
/// Eşlemeler <see cref="AskQuestionCommandHandler"/>'dan ayrılmıştır; handler böylece yalnızca hattın adımlarını ve
/// kararlarını anlatır. Eşlemeler durumsuz ve yan etkisizdir.
/// </remarks>
internal static class AnswerMapper
{
    /// <summary>
    /// <see cref="VersionResolution"/> sonucunu yanıttaki <c>versionResolution</c> alanına çevirir; yalnızca
    /// <paramref name="relevantFamilies"/> içindeki, yani yanıtın atıf yaptığı doküman ailelerinin seçilen ve elenen
    /// sürümlerini, elenme gerekçeleriyle birlikte taşır. <c>Applied</c>, bu süzmeden sonra en az bir sürüm elenmişse
    /// true olur.
    /// </summary>
    /// <remarks>
    /// Görevin "kaynaklar çeliştiğinde güncel sürümün nasıl seçildiğini göster" şartı bu alanla karşılanır. Süzme
    /// olmasaydı, örneğin kargo ücreti sorusunda bağlama giren ama yanıtta kullanılmayan iade politikasının sürüm kararı
    /// da raporlanır ve kullanıcı yanıtın yanlış bir dokümana dayandığını düşünebilirdi. Kural metni
    /// (<see cref="VersionResolver.Rule"/>) her zaman eklenir; seçimin hangi kurala göre yapıldığı yanıtın içinden okunur.
    /// </remarks>
    public static VersionResolutionDto ToVersionResolutionDto(VersionResolution resolution, IReadOnlySet<string> relevantFamilies)
    {
        var discarded = resolution.Discarded
            .Where(item => relevantFamilies.Contains(item.Version.DocumentKey))
            .Select(item => new DiscardedVersionDto(item.Version.DocumentId, item.Version.Title, item.Version.Version, item.Version.EffectiveDate, item.Reason))
            .ToList();

        var selected = resolution.Selected
            .Where(version => relevantFamilies.Contains(version.DocumentKey))
            .Select(version => new VersionRefDto(version.DocumentId, version.Title, version.Version, version.EffectiveDate))
            .ToList();

        return new VersionResolutionDto(discarded.Count > 0, VersionResolver.Rule, selected, discarded);
    }

    /// <summary>
    /// Kabul edilmiş (etiketi bağlamda, alıntısı doğrulanmış) bir atıfı yanıttaki <c>sources</c> öğesine çevirir: doküman
    /// kimliği, başlık, sürüm, yürürlük tarihi, durum, tür, bölüm yolu, alıntı ve <c>quoteVerified</c>. Görevin "her yanıt
    /// kullandığı dokümanı ve ilgili bölümü göstermeli" şartı bu alanlarla karşılanır. Yalnızca doğrulanmış atıflar kaynak
    /// olduğundan <c>quoteVerified</c> burada her zaman true'dur; alan, sözleşmenin açık kalması ve istemcinin bunu
    /// kendisi de denetleyebilmesi için taşınır.
    /// </summary>
    public static AnswerSourceDto ToSourceDto(ValidatedCitation citation)
    {
        var chunk = citation.Source.Chunk;
        return new AnswerSourceDto(
            chunk.DocumentId,
            chunk.Title,
            chunk.Version,
            chunk.EffectiveDate,
            chunk.Status.ToApi(),
            chunk.Category.ToApi(),
            chunk.SectionPath,
            citation.Quote,
            citation.QuoteVerified);
    }

    /// <summary>
    /// Yanıtın <c>diagnostics</c> bölümünü oluşturur: arama modu (hybrid/lexical), Kapı 1'in baktığı iki sinyal (en iyi
    /// kosinüs benzerliği ve en iyi sözcük kapsamı), sürüm çözümlemesinden önce bulunan doküman kimlikleri, modele
    /// etiketleriyle verilen bölümler, model adı, gecikme, model çağrı sayısı ve varsa toplam token sayıları.
    /// </summary>
    /// <remarks>
    /// Hem yanıtlarda hem retlerde doldurulur; "neden reddedildi, neden bu kaynak?" soruları ancak bu verilerle
    /// cevaplanabilir. Model adı, adaptörün döndürdüğü <c>GeneratedAnswer.Model</c> değeridir, yani yapılandırılmış model
    /// adıdır (llama.cpp'nin bildirdiği kimlik yerel model dosyasının yolunu içerdiği için kullanılmaz); model çağrılmadan
    /// verilen retlerde boştur. <paramref name="usage"/> da bu yüzden null olabilir; o durumda çağrı sayısı 0'dır ve token
    /// sayısı yoktur. Gecikme, kronometrenin başladığı andan (iş kurallarından sonra) bu metodun çağrıldığı ana kadar geçen
    /// süredir.
    /// </remarks>
    public static AnswerDiagnosticsDto Diagnostics(
        SearchResult retrieval,
        IReadOnlyList<string> candidateDocumentIds,
        IReadOnlyList<ContextChunk> context,
        string model,
        Stopwatch stopwatch,
        ModelUsage? usage) => new(
        retrieval.Mode.ToApi(),
        retrieval.MaxDenseScore,
        retrieval.MaxLexicalCoverage,
        candidateDocumentIds,
        context.Select(source => new ContextSourceDto(source.Label, source.Chunk.DocumentId, source.Chunk.Version, source.Chunk.SectionPath)).ToList(),
        model,
        stopwatch.ElapsedMilliseconds,
        usage?.InputTokens,
        usage?.OutputTokens,
        usage?.Calls ?? 0);
}
