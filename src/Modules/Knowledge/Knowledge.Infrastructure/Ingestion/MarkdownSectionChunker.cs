using System.Text;
using System.Text.RegularExpressions;
using Knowledge.Application.Abstractions;

namespace Knowledge.Infrastructure.Ingestion;

/// <summary>
/// Splits a markdown body into one chunk per heading. A chunk carries its heading path
/// ("2. Destek Seviyeleri > 2.2 Seviye 2 (L2)") so answers can cite the exact section.
/// </summary>
public static partial class MarkdownSectionChunker
{
    public const int DefaultMaxChunkChars = 1200;

    public static IReadOnlyList<SourceSection> Split(string markdownBody, string documentTitle, int maxChunkChars = DefaultMaxChunkChars)
    {
        var sections = new List<SourceSection>();
        var headings = new List<(int Level, string Text)>();
        var buffer = new StringBuilder();

        foreach (var line in markdownBody.Replace("\r\n", "\n").Split('\n'))
        {
            var heading = HeadingPattern().Match(line);

            if (!heading.Success)
            {
                buffer.Append(line).Append('\n');
                continue;
            }

            Flush();

            var level = heading.Groups[1].Value.Length;
            headings.RemoveAll(existing => existing.Level >= level);
            headings.Add((level, heading.Groups[2].Value.Trim()));
        }

        Flush();
        return sections;

        void Flush()
        {
            var content = buffer.ToString().Trim();
            buffer.Clear();

            // Headings that only group sub-sections carry no text of their own.
            if (content.Length == 0)
            {
                return;
            }

            // The level-1 heading is the document title, which every source already shows.
            var pathParts = headings.Where(existing => existing.Level > 1).Select(existing => existing.Text).ToList();
            var path = pathParts.Count > 0 ? string.Join(" > ", pathParts) : documentTitle;

            foreach (var part in SplitAtParagraphs(content, maxChunkChars))
            {
                sections.Add(new SourceSection(path, part));
            }
        }
    }

    // A single paragraph longer than the limit is kept whole: cutting mid-paragraph would split a rule from its condition.
    private static IEnumerable<string> SplitAtParagraphs(string content, int maxChunkChars)
    {
        if (content.Length <= maxChunkChars)
        {
            yield return content;
            yield break;
        }

        var current = new StringBuilder();

        foreach (var paragraph in ParagraphSeparator().Split(content).Select(part => part.Trim()).Where(part => part.Length > 0))
        {
            if (current.Length > 0 && current.Length + 2 + paragraph.Length > maxChunkChars)
            {
                yield return current.ToString();
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append("\n\n");
            }

            current.Append(paragraph);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*#*\s*$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"\n[ \t]*\n")]
    private static partial Regex ParagraphSeparator();
}
