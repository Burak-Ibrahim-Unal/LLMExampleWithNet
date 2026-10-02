using Knowledge.Application.Abstractions;

namespace Knowledge.Infrastructure.Embeddings;

/// <summary>
/// Embedding ucu yapılandırılmadığında (<c>Embeddings:BaseUrl</c> boş) kullanılan Null Object; indeks bu durumda
/// yalnızca BM25 ile çalışır. Port'un her zaman bir uygulaması olduğu için ingest, indeks ve sağlık uç noktası her
/// yerde null kontrolü yapmak yerine yalnızca <see cref="IsEnabled"/> değerine bakar.
/// </summary>
public sealed class DisabledTextEmbedder : ITextEmbedder
{
    /// <summary>
    /// Her zaman <c>false</c>. Ingest bu durumda embedding adımını atlar, indeks vektör aramasını kapatır ve sağlık uç
    /// noktası embedding bileşenini yapılandırılmamış olarak raporlar.
    /// </summary>
    public bool IsEnabled => false;

    /// <summary>Boş ad: yapılandırılmış bir embedding modeli yoktur; sağlık yanıtında model adı boş görünür.</summary>
    public string ModelName => string.Empty;

    /// <summary>
    /// Çağrılmamalıdır; çağıranlar önce <see cref="IsEnabled"/> değerine bakar. Boş vektör döndürüp sessizce devam
    /// etmek yerine istisna fırlatmak, bu sözleşmeyi bozan bir kod yolunu hemen görünür kılar ve anlamsız vektörlerin
    /// veritabanına yazılmasını önler.
    /// </summary>
    public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<DocumentEmbeddingInput> inputs, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Embeddings are disabled.");

    /// <summary>
    /// Çağrılmamalıdır: embedding kapalıyken indeks vektör tutmadığı için sorgu embedding'i istemez. Yine de çağrılırsa
    /// fırlatılan istisna indeksin hata yakalamasıyla BM25 aramasına dönüşür.
    /// </summary>
    public Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Embeddings are disabled.");
}
