using Knowledge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knowledge.Infrastructure.Persistence;

public sealed class QuestionLogConfiguration : IEntityTypeConfiguration<QuestionLog>
{
    public void Configure(EntityTypeBuilder<QuestionLog> builder)
    {
        builder.ToTable("question_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Question).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RefusalReason).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ResponseJson).IsRequired();

        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
