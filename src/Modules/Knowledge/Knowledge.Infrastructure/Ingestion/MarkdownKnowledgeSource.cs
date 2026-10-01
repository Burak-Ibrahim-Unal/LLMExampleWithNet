using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Ingestion;

/// <summary>Reads every *.md file of the knowledge base folder; the files are the source of truth.</summary>
public sealed class MarkdownKnowledgeSource(IOptions<KnowledgeBaseOptions> options) : IKnowledgeBaseSource
{
    public async Task<IReadOnlyList<SourceDocument>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var folder = ResolveFolder(options.Value.Path);
        var documents = new List<SourceDocument>();

        foreach (var file in Directory.EnumerateFiles(folder, "*.md").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var text = await File.ReadAllTextAsync(file, cancellationToken);
            documents.Add(MarkdownDocumentParser.Parse(Path.GetFileName(file), text));
        }

        return documents;
    }

    /// <summary>
    /// A relative path is looked up in the working directory and its parents, then in the application
    /// directory and its parents, so "knowledge-base" resolves from the repository root, the project folder or bin/.
    /// </summary>
    private static string ResolveFolder(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Directory.Exists(configuredPath) ? configuredPath : throw Missing(configuredPath);
        }

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, configuredPath);

                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw Missing(configuredPath);
    }

    private static KnowledgeBaseFormatException Missing(string path) => new($"Bilgi tabanı klasörü bulunamadı: {path}");
}
