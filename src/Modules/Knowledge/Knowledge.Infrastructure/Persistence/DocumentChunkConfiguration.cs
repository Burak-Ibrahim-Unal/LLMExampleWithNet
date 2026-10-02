using Knowledge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Knowledge.Infrastructure.Persistence;

/// <summary>
/// <see cref="DocumentChunk"/> varlığını (bir doküman bölümü = bir chunk; cevapların kaynak olarak gösterdiği birim)
/// SQLite'taki <c>document_chunks</c> tablosuna eşler.
/// </summary>
/// <remarks>
/// Eşleme domain modelinin dışında, altyapı katmanında durur: Knowledge.Domain EF Core'a bağımlı değildir. Sınıf,
/// <c>AppDbContext</c> tarafından <c>ApplyConfigurationsFromAssembly</c> ile otomatik bulunur (bkz.
/// <c>AssemblyReference</c>).
/// </remarks>
public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    /// <summary>
    /// Tabloyu, anahtarı, alan sınırlarını, embedding vektörünün BLOB dönüşümünü ve <c>(DocumentId, Order)</c>
    /// indeksini yapılandırır.
    /// </summary>
    /// <remarks>
    /// En kritik satır <c>ValueGeneratedNever</c>'dır: id'ler domain'de atanır. Bu ayar yokken, değişen bir doküman
    /// yeniden chunk'lanırken izlenen (tracked) dokümanın koleksiyonuna eklenen yeni chunk'ları EF, anahtarları dolu
    /// olduğu için mevcut satır sanıyor, INSERT yerine UPDATE gönderiyor ve güncellenecek satır bulunamayınca
    /// <c>DbUpdateConcurrencyException</c> fırlatıyordu (geliştirme sırasında yakalanıp düzeltilen hata).
    /// </remarks>
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(x => x.Id);

        // Id'ler EntityBase oluşturulurken (Guid.NewGuid ile) atanır. Bu satır olmadan EF Core, izlenen bir dokümanın
        // koleksiyonuna eklenen yeni chunk'ı (anahtarı zaten dolu olduğu için) mevcut bir satır sanır ve INSERT yerine
        // UPDATE gönderir; güncellenecek satır olmadığından kayıt DbUpdateConcurrencyException ile başarısız olur.
        builder.Property(x => x.Id).ValueGeneratedNever();

        // SectionPath, cevaplarda gösterilen başlık yoludur (ör. "2. İade Süresi"). EmbeddingModel, vektörü üreten modelin
        // adıdır: farklı modellerin vektörleri karşılaştırılamaz; ingestion yeniden embedding gerekip gerekmediğine, indeks
        // de saklanan vektörleri kullanıp kullanmayacağına bu alana bakarak karar verir.
        builder.Property(x => x.SectionPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.EmbeddingModel).HasMaxLength(200);

        // Vektör, JSON metni yerine boyut başına 4 bayt tutan bir BLOB olarak saklanır (bkz. EmbeddingBlob).
        // EF Core bir dönüştürücüye (converter) asla null geçirmez — null değer doğrudan NULL olarak yazılır — bu yüzden
        // buradaki null-forgiving operatörü (!) güvenlidir.
        // ValueComparer gereklidir: dizi bir referans tipidir; varsayılan karşılaştırma referans eşitliğine dayanır ve
        // değişiklik takibinin anlık görüntüsü (snapshot) aynı diziyi paylaşır, dizinin içi yerinde değişirse EF bunu
        // fark etmez. Buradaki karşılaştırıcı içerik eşitliği (SequenceEqual), ucuz bir hash (uzunluk) ve kopyalanmış
        // snapshot (ToArray) tanımlar.
        builder.Property(x => x.Embedding)
            .HasConversion(
                new ValueConverter<float[]?, byte[]>(vector => EmbeddingBlob.ToBytes(vector!), bytes => EmbeddingBlob.FromBytes(bytes)),
                new ValueComparer<float[]?>(
                    (left, right) => ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
                    vector => vector == null ? 0 : vector.Length,
                    vector => vector == null ? null : vector.ToArray()));

        // Bir dokümanın chunk'ları her zaman DocumentId üzerinden yüklenir veya silinir (Include, cascade delete); Order ise
        // bölümün doküman içindeki sırasıdır. Bileşik indeks bu erişimi bölüm sırasıyla birlikte karşılar.
        builder.HasIndex(x => new { x.DocumentId, x.Order });
    }
}
