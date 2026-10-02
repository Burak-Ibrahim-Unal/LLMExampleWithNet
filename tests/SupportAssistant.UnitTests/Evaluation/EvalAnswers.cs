using Knowledge.Application.Contracts;

namespace SupportAssistant.UnitTests.Evaluation;

/// <summary>
/// Değerlendirme testlerinin ortak yanıt kurucusu: kontrollerin okuduğu alanlar dışında sabit değerler taşıyan bir
/// <c>AnswerDto</c>. Test sınıfları bunu <c>using static</c> ile kullanır.
/// </summary>
internal static class EvalAnswers
{
    /// <summary>
    /// Kontrollerin okuduğu alanlar dışında sabit değerler taşıyan bir API yanıtı kurar: yanıt metni, <c>answerable</c>
    /// bayrağı, atıf yapılan doküman kimlikleri (aynı bölüm adı ve alıntı doğrulama durumuyla), elenen sürüm kimlikleri,
    /// çelişkiler ve ret gerekçesi. Ret gerekçesi verilmezse yanıtlanamayan yanıtlarda <c>LowRelevance</c> kullanılır.
    /// </summary>
    public static AnswerDto Answer(
        string text,
        bool answerable = true,
        string[]? sources = null,
        string[]? discarded = null,
        string section = "Bölüm",
        bool quoteVerified = true,
        IReadOnlyList<ConflictDto>? conflicts = null,
        string? refusalReason = null) => new(
        "soru",
        answerable,
        text,
        (sources ?? []).Select(id => new AnswerSourceDto(id, "Başlık", "1.0", new DateOnly(2025, 1, 1), "active", "politika", section, "alıntı", quoteVerified)).ToList(),
        new VersionResolutionDto(
            (discarded ?? []).Length > 0,
            "kural",
            [],
            (discarded ?? []).Select(id => new DiscardedVersionDto(id, "Başlık", "1.0", new DateOnly(2024, 1, 1), "gerekçe")).ToList()),
        conflicts ?? [],
        string.Empty,
        refusalReason ?? (answerable ? string.Empty : "LowRelevance"),
        new AnswerDiagnosticsDto("hybrid", 0.7, 1.0, [], [], "model", 100, null, null, 1));
}
