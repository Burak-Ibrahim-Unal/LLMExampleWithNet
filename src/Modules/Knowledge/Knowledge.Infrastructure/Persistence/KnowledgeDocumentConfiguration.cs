using Knowledge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Knowledge.Infrastructure.Persistence;

/// <summary>
/// <see cref="KnowledgeDocument"/> toplam kökünü (aggregate root; bir dokümanın tek bir sürümü)
/// <c>knowledge_documents</c> tablosuna eşler ve chunk koleksiyonuyla ilişkisini kurar.
/// </summary>
/// <remarks>
/// Veritabanı, <c>knowledge-base/*.md</c> dosyalarından yeniden üretilen türetilmiş veridir. Bu eşleme, ingestion'ın
/// değişmeyen dokümanları yeniden embed etmeden tanıyabilmesi için gereken alanları (<c>SourceId</c>,
/// <c>ContentHash</c>) güvenilir biçimde saklar. SQLite metin uzunluklarını zorlamaz; <c>HasMaxLength</c> burada
/// şemayı belgeler ve başka bir veritabanı sağlayıcısına geçildiğinde gerçek bir kısıta dönüşür.
/// </remarks>
public sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    /// <summary>
    /// Anahtarı, metin alanlarının sınırlarını, enum'ların metin olarak saklanmasını, indeksleri ve <c>Chunks</c>
    /// ilişkisini (cascade delete ve private alan üzerinden erişim) yapılandırır.
    /// </summary>
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("knowledge_documents");

        // Id domain'de atanır, veritabanı üretmez. DocumentChunk'taki aynı ayar gibi, EF'in anahtarı dolu yeni bir nesneyi
        // mevcut satır sanmasını önler ve niyeti açıkça belgeler.
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // SourceId, front matter'daki kararlı kimliktir (ör. "iade-politikasi-v2"): ingestion uzlaştırması ve
        // GET /v1/documents/{id} bu alanla arar. Benzersiz indeks, iş kuralındaki tekrar kontrolünün (CheckUniqueDocumentIds)
        // arkasındaki veritabanı düzeyindeki son savunma hattıdır.
        builder.Property(x => x.SourceId).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.SourceId).IsUnique();

        // DocumentKey doküman ailesidir (ör. "iade-politikasi"): aynı prosedürün eski ve güncel sürümleri bu anahtarı
        // paylaşır. Sürüm kataloğu şu an bellekteki indekste kurulur; indeks aile bazlı sorgular için hazır tutulur.
        builder.Property(x => x.DocumentKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.DocumentKey);

        // Metadata alanları front matter'dan gelir. Status ve Category sayı yerine metin ("Superseded", "Policy") olarak
        // saklanır: veritabanı okunabilir kalır ve enum üyelerinin sırası değişse bile kayıtlı değerler anlamını korur.
        // ContentHash, normalize edilmiş dosya metninin SHA-256 özetidir (64 hex karakter); ingestion değişmeyen dokümanları
        // bununla tanır. Supersedes isteğe bağlıdır, bu yüzden sütun NULL olabilir.
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(20).IsRequired();
        builder.Property(x => x.EffectiveDate).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Supersedes).HasMaxLength(200);
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();

        // Chunk'lar dokümana aittir: doküman silinince (kaynak dosya kaldırıldığında) chunk'ları da silinir. İlişki zorunlu
        // olduğundan ClearChunks ile koleksiyondan çıkarılan chunk'lar da sahipsiz (orphan) kalır ve silinir; değişen bir
        // dokümanın eski bölümleri böylece temizlenir.
        builder.HasMany(x => x.Chunks)
            .WithOne()
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Domain koleksiyonu dışarıya salt okunur IReadOnlyList olarak açar; EF yükleme sırasında doğrudan private _chunks
        // alanına yazar. Böylece koleksiyon yalnızca AddChunk/ClearChunks ile değiştirilebilir ve kapsülleme korunur.
        builder.Navigation(x => x.Chunks)
            .HasField("_chunks")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
