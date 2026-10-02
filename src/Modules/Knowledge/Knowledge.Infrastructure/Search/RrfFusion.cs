namespace Knowledge.Infrastructure.Search;

/// <summary>
/// Reciprocal Rank Fusion (RRF): puan(öğe) = Σ 1 / (k + sıra). Sıralamaları, BM25 puanlarını kosinüs benzerlikleriyle
/// kalibre etmeye gerek kalmadan birleştirir; bu iki ölçü birbiriyle ilgisiz ölçeklerde yaşar.
/// </summary>
/// <remarks>
/// BM25 üstten sınırsız ve sorgudan sorguya değişen bir puandır, kosinüs ise sınırlı bir aralıktadır; ağırlıklı toplam
/// gibi puan tabanlı bir birleştirme, sorgudan sorguya kayan normalizasyon katsayıları gerektirirdi. RRF yalnızca sıraları
/// kullandığı için böyle bir ayar gerekmez ve birden fazla listede üst sıralarda yer alan öğeyi ödüllendirir.
/// </remarks>
public static class RrfFusion
{
    /// <summary>
    /// Sıralı listeleri (her biri en iyiden başlayan öğe kimlikleri) RRF ile tek bir sıralamada birleştirir; öğeleri
    /// birleşik puana göre azalan sırada döndürür.
    /// </summary>
    /// <param name="rankedLists">Birleştirilecek sıralamalar; burada BM25 ve (varsa) vektör aday listeleri.</param>
    /// <param name="k">
    /// Sönümleme sabiti. 60, RRF'yi öneren çalışmada (Cormack vd., 2009) kullanılan ve yaygınlaşan değerdir; büyük bir k,
    /// tek bir listenin ilk sırasının sonucu domine etmesini engeller ve listeler arası uzlaşmayı öne çıkarır.
    /// </param>
    /// <remarks>
    /// Sıra 1'den başlar (konum + 1). Eşit puanlı öğeler ilk görüldükleri sıraya göre dizilir; <c>KnowledgeIndex</c> BM25
    /// listesini önce verdiğinden eşitlikte BM25 sırası korunur. Böylece sonuç, sözlük (dictionary) gezinme sırası gibi
    /// garanti edilmeyen bir ayrıntıya bağlı kalmadan deterministik olur.
    /// </remarks>
    public static IReadOnlyList<(int Item, double Score)> Fuse(IReadOnlyList<IReadOnlyList<int>> rankedLists, int k = 60)
    {
        var scores = new Dictionary<int, double>();
        var firstSeen = new Dictionary<int, int>();

        foreach (var list in rankedLists)
        {
            for (var position = 0; position < list.Count; position++)
            {
                var item = list[position];
                scores[item] = scores.GetValueOrDefault(item) + 1.0 / (k + position + 1);
                firstSeen.TryAdd(item, firstSeen.Count);
            }
        }

        return scores
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => firstSeen[entry.Key])
            .Select(entry => (entry.Key, entry.Value))
            .ToList();
    }
}
