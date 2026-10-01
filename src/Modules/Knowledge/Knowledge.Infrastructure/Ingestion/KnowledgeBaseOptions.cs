namespace Knowledge.Infrastructure.Ingestion;

public sealed class KnowledgeBaseOptions
{
    public const string SectionName = "KnowledgeBase";

    /// <summary>Folder of markdown documents. A relative path is searched upwards from the working and application directories.</summary>
    public string Path { get; set; } = "knowledge-base";
}
