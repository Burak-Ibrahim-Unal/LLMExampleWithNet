namespace Knowledge.Application.Contracts;

/// <param name="Id">Document identifier from the front matter, e.g. "iade-politikasi-v2".</param>
/// <param name="DocumentKey">Shared by every version of the same procedure.</param>
/// <param name="Status">"active" or "superseded".</param>
/// <param name="Category">"politika", "prosedur", "kilavuz" or "sss".</param>
public sealed record DocumentSummaryDto(
    string Id,
    string DocumentKey,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    string Status,
    string Category,
    string? Supersedes,
    int SectionCount);

public sealed record DocumentDetailDto(
    string Id,
    string DocumentKey,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    string Status,
    string Category,
    string? Supersedes,
    IReadOnlyList<DocumentSectionDto> Sections);

public sealed record DocumentSectionDto(int Order, string Section, string Content);
