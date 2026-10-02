using System.Runtime.InteropServices;

namespace Knowledge.Infrastructure.Persistence;

/// <summary>
/// Embedding vektörlerinin JSON metni yerine kompakt BLOB'lar (boyut başına 4 bayt) olarak saklanması için
/// <c>float[]</c> ↔ <c>byte[]</c> dönüşümü yapar; SQLite BLOB sütunu için EF değer dönüştürücüsü bunu kullanır.
/// </summary>
/// <remarks>
/// bge-m3 ile her chunk vektörü 1024 boyutludur: BLOB olarak tam 4 KB tutar, metin olarak birkaç kat daha fazla yer
/// kaplardı. Dönüşüm bit düzeyinde birebirdir; ondalık sayıları metne çevirip geri okumanın maliyeti ve yuvarlama kaybı
/// yoktur. SQLite'ın yerel bir vektör tipi olmadığından en sade ve kayıpsız temsil budur. Bayt sırası çalışılan
/// platformun yerel sırasıdır (pratikte little-endian); veritabanı Markdown dosyalarından yeniden üretilebilen
/// türetilmiş veri olduğundan farklı bayt sırasına sahip bir makineye taşınabilirlik hedeflenmez.
/// </remarks>
public static class EmbeddingBlob
{
    /// <summary>
    /// Vektörün bellekteki ham baytlarını, eleman eleman dönüştürmeden tek bir kopyayla bayt dizisine çevirir. EF,
    /// <c>DocumentChunk.Embedding</c> değerini veritabanına yazarken kullanır.
    /// </summary>
    public static byte[] ToBytes(float[] vector) => MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();

    /// <summary>
    /// <see cref="ToBytes"/> çıktısını yeniden <c>float[]</c>'a çevirir; EF satırı okurken kullanır. Boyut bayt
    /// uzunluğundan (uzunluk / 4) türetildiği için ayrıca saklanması gerekmez.
    /// </summary>
    public static float[] FromBytes(byte[] bytes) => MemoryMarshal.Cast<byte, float>(bytes).ToArray();
}
