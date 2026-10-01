using Knowledge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knowledge.Infrastructure.Persistence;

public sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("knowledge_documents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SourceId).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.SourceId).IsUnique();

        builder.Property(x => x.DocumentKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.DocumentKey);

        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(20).IsRequired();
        builder.Property(x => x.EffectiveDate).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Supersedes).HasMaxLength(200);
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();

        builder.HasMany(x => x.Chunks)
            .WithOne()
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Chunks)
            .HasField("_chunks")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
