using Knowledge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Knowledge.Infrastructure.Persistence;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(x => x.Id);

        // Ids are assigned in EntityBase's constructor. Without this, EF Core treats a new chunk added to a
        // tracked document's collection as an existing row (key already set) and issues an UPDATE instead of an INSERT.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SectionPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.EmbeddingModel).HasMaxLength(200);

        // EF Core never passes null to a converter, so the null-forgiving operator is safe here.
        builder.Property(x => x.Embedding)
            .HasConversion(
                new ValueConverter<float[]?, byte[]>(vector => EmbeddingBlob.ToBytes(vector!), bytes => EmbeddingBlob.FromBytes(bytes)),
                new ValueComparer<float[]?>(
                    (left, right) => ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
                    vector => vector == null ? 0 : vector.Length,
                    vector => vector == null ? null : vector.ToArray()));

        builder.HasIndex(x => new { x.DocumentId, x.Order });
    }
}
