using Shared.Kernel.Abstractions;

namespace Knowledge.Domain.Entities;

/// <summary>A searchable section of a document; the unit that answers cite.</summary>
public sealed class DocumentChunk : EntityBase
{
    private DocumentChunk()
    {
    }

    internal DocumentChunk(Guid documentId, int order, string sectionPath, string content)
    {
        DocumentId = documentId;
        Order = order;
        SectionPath = sectionPath;
        Content = content;
    }

    public Guid DocumentId { get; private set; }

    public int Order { get; private set; }

    /// <summary>Heading path inside the document, e.g. "2. Destek Seviyeleri > 2.2 Seviye 2 (L2)".</summary>
    public string SectionPath { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public float[]? Embedding { get; private set; }

    /// <summary>Model that produced <see cref="Embedding"/>; vectors from another model are not comparable.</summary>
    public string? EmbeddingModel { get; private set; }

    public void SetEmbedding(float[] embedding, string model)
    {
        Embedding = embedding;
        EmbeddingModel = model;
    }
}
