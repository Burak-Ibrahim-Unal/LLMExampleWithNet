using Knowledge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knowledge.Infrastructure.Persistence;

/// <summary>
/// <see cref="QuestionLog"/> denetim (audit) kaydını <c>question_logs</c> tablosuna eşler.
/// </summary>
/// <remarks>
/// Her soru — cevaplanan da reddedilen de — istemciye gönderilen yanıtın tamamıyla birlikte saklanır; böylece bir
/// cevabın hangi kaynaklarla verildiği ya da neden reddedildiği sonradan incelenebilir ve değerlendirme sonuçları
/// doğrulanabilir.
/// </remarks>
public sealed class QuestionLogConfiguration : IEntityTypeConfiguration<QuestionLog>
{
    /// <summary>
    /// Anahtarı (domain'de atanan Guid), alan sınırlarını ve oluşturulma zamanı indeksini tanımlar.
    /// </summary>
    public void Configure(EntityTypeBuilder<QuestionLog> builder)
    {
        builder.ToTable("question_logs");

        // Id domain'de atanır; diğer varlıklarla tutarlı olarak veritabanı değer üretmez.
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // Soru API'de 500 karakterle sınırlıdır (MaxQueryLength); 1000 güvenli bir pay bırakır. RefusalReason cevaplanan
        // sorularda null değil boş metindir. Model, yanıtı üreten yapılandırılmış sohbet modelinin adıdır (model çağrılmadan
        // verilen retlerde boştur). ResponseJson, kaynaklar, sürüm kararları ve tanılama bilgisiyle birlikte istemciye giden
        // yanıtın tamamıdır.
        builder.Property(x => x.Question).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RefusalReason).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ResponseJson).IsRequired();

        // Denetim kayıtları zamana göre incelenir (ör. son sorular, belirli bir zaman aralığı); indeks bu sorguları hızlandırır.
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
