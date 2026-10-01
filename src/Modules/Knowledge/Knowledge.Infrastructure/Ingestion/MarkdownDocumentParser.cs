using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;
using Knowledge.Domain.Entities;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Knowledge.Infrastructure.Ingestion;

/// <summary>Parses one knowledge base file: YAML front matter (metadata) followed by a markdown body.</summary>
public static class MarkdownDocumentParser
{
    private const string Delimiter = "---";

    private static readonly IDeserializer FrontMatterDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static SourceDocument Parse(string fileName, string text, int maxChunkChars = MarkdownSectionChunker.DefaultMaxChunkChars)
    {
        // Line endings depend on the checkout (CRLF on Windows); hashing the normalized text keeps
        // "unchanged document" detection stable across machines.
        var normalized = text.TrimStart('﻿').Replace("\r\n", "\n");

        if (!normalized.StartsWith(Delimiter + "\n", StringComparison.Ordinal))
        {
            throw Invalid(fileName, "dosya YAML front matter ('---') ile başlamalı");
        }

        var closing = normalized.IndexOf("\n" + Delimiter + "\n", Delimiter.Length, StringComparison.Ordinal);

        if (closing < 0)
        {
            throw Invalid(fileName, "front matter kapanış satırı ('---') bulunamadı");
        }

        // "---\n---\n": the closing delimiter directly follows the opening one, i.e. the front matter is empty.
        var yaml = closing > Delimiter.Length ? normalized[(Delimiter.Length + 1)..closing] : string.Empty;
        var frontMatter = ReadFrontMatter(fileName, yaml);
        var body = normalized[(closing + Delimiter.Length + 2)..];

        var title = Required(fileName, frontMatter.Title, "title");
        var sections = MarkdownSectionChunker.Split(body, title, maxChunkChars);

        if (sections.Count == 0)
        {
            throw Invalid(fileName, "içerik bölümü bulunamadı");
        }

        return new SourceDocument(
            SourceId: Required(fileName, frontMatter.Id, "id"),
            DocumentKey: Required(fileName, frontMatter.DocumentKey, "documentKey"),
            Title: title,
            Version: Required(fileName, frontMatter.Version, "version"),
            EffectiveDate: ParseDate(fileName, Required(fileName, frontMatter.EffectiveDate, "effectiveDate")),
            Status: ParseStatus(fileName, Required(fileName, frontMatter.Status, "status")),
            Category: ParseCategory(fileName, Required(fileName, frontMatter.Category, "category")),
            Supersedes: string.IsNullOrWhiteSpace(frontMatter.Supersedes) ? null : frontMatter.Supersedes.Trim(),
            ContentHash: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))),
            Sections: sections);
    }

    private static FrontMatter ReadFrontMatter(string fileName, string yaml)
    {
        try
        {
            return FrontMatterDeserializer.Deserialize<FrontMatter>(yaml) ?? new FrontMatter();
        }
        catch (YamlException exception)
        {
            throw Invalid(fileName, $"front matter okunamadı ({exception.Message})");
        }
    }

    private static string Required(string fileName, string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw Invalid(fileName, $"zorunlu front matter alanı eksik: {field}")
            : value.Trim();
    }

    private static DateOnly ParseDate(string fileName, string value)
    {
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw Invalid(fileName, $"effectiveDate 'yyyy-MM-dd' biçiminde olmalı: {value}");
    }

    private static DocumentStatus ParseStatus(string fileName, string value)
    {
        return value.ToLowerInvariant() switch
        {
            "active" => DocumentStatus.Active,
            "superseded" => DocumentStatus.Superseded,
            _ => throw Invalid(fileName, $"bilinmeyen status: {value} (active | superseded)")
        };
    }

    private static DocumentCategory ParseCategory(string fileName, string value)
    {
        return value.ToLowerInvariant() switch
        {
            "politika" => DocumentCategory.Policy,
            "prosedur" => DocumentCategory.Procedure,
            "kilavuz" => DocumentCategory.Guide,
            "sss" => DocumentCategory.Faq,
            _ => throw Invalid(fileName, $"bilinmeyen category: {value} (politika | prosedur | kilavuz | sss)")
        };
    }

    private static KnowledgeBaseFormatException Invalid(string fileName, string reason) => new($"{fileName}: {reason}");

    private sealed class FrontMatter
    {
        public string? Id { get; set; }

        public string? DocumentKey { get; set; }

        public string? Title { get; set; }

        public string? Version { get; set; }

        public string? EffectiveDate { get; set; }

        public string? Status { get; set; }

        public string? Supersedes { get; set; }

        public string? Category { get; set; }
    }
}
