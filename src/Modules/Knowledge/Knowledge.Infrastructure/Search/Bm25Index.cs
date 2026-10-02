namespace Knowledge.Infrastructure.Search;

/// <summary>
/// Bir chunk'ın sözcüksel (BM25) eşleşme sonucu.
/// </summary>
/// <param name="DocumentIndex">Chunk'ın <see cref="Bm25Index"/>'e verilen listedeki konumu (indeksteki chunk sırası).</param>
/// <param name="Score">Ham BM25 puanı; yalnızca aynı sorgunun sonuçlarını sıralamak için anlamlıdır.</param>
/// <param name="Coverage">Sorgu terimlerinin idf ağırlıklı ne kadarının chunk'ta bulunduğu (0..1); Kapı 1'in girdisi.</param>
/// <remarks>
/// Değer tipi (<c>readonly record struct</c>) olduğundan varsayılan değeri (Score = 0, Coverage = 0) doğal olarak
/// "sözcüksel eşleşme yok" anlamına gelir; <c>KnowledgeIndex</c>, yalnızca vektör aramasından gelen bir chunk için bunu
/// null kontrolü yapmadan kullanır. Ayrıca her eşleşme için heap ayırması yapılmaz.
/// </remarks>
public readonly record struct LexicalMatch(int DocumentIndex, double Score, double Coverage);

/// <summary>
/// Önceden terimlere ayrılmış dokümanlar (burada: bilgi tabanı chunk'ları) üzerinde Okapi BM25 indeksi; Kapı 1 için idf
/// ağırlıklı kapsama (coverage) ölçüsünü de hesaplar.
/// </summary>
/// <remarks>
/// BM25 her zaman açık olan arama yoludur: embedding sunucusu yokken de çalışır ve "750 TL", "2.4 GHz" gibi sayı ya da
/// terim düzeyindeki tam eşleşmelerde embedding'den güçlüdür. Bir kez kurulur ve değişmez; <c>KnowledgeIndex</c> yeniden
/// indekslemede yeni bir örnek oluşturduğundan eşzamanlı okumalar kilit gerektirmez. Onlarca chunk için ters indeks
/// (inverted index) kurmak yerine her chunk'ın terim tablosunu taramak yeterince hızlıdır.
/// </remarks>
public sealed class Bm25Index
{
    /// <summary>Terim frekansı doyma parametresi: bir terimin chunk'ta tekrar etmesinin puana katkısı bu değere göre doyar.</summary>
    private readonly double _k1;
    /// <summary>Uzunluk normalizasyonu ağırlığı (0 = yok, 1 = tam); uzun chunk'ların yalnızca uzun oldukları için öne geçmesini engeller.</summary>
    private readonly double _b;
    /// <summary>Ortalama chunk uzunluğu (terim sayısı); uzunluk normalizasyonunun referans noktası.</summary>
    private readonly double _averageLength;
    /// <summary>Her chunk'ın terim sayısı.</summary>
    private readonly int[] _lengths;
    /// <summary>Her chunk için terim → o chunk'taki tekrar sayısı.</summary>
    private readonly Dictionary<string, int>[] _termFrequencies;
    /// <summary>Terim → o terimi içeren chunk sayısı (doküman frekansı, df); idf hesabının girdisi.</summary>
    private readonly Dictionary<string, int> _documentFrequencies = new(StringComparer.Ordinal);

    /// <summary>
    /// Her chunk için terim frekanslarını, terimlerin kaç chunk'ta geçtiğini ve ortalama uzunluğu bir kez hesaplar; sorgu
    /// anında yalnızca bu tablolar okunur.
    /// </summary>
    /// <param name="documents">Terimlere ayrılmış chunk'lar; listedeki konum <see cref="LexicalMatch.DocumentIndex"/> olur.</param>
    /// <param name="k1">Terim frekansı doyma parametresi; 1.2, Lucene ve Elasticsearch'ün de kullandığı yaygın varsayılandır.</param>
    /// <param name="b">Uzunluk normalizasyonu ağırlığı; 0.75 yaygın varsayılandır.</param>
    /// <remarks>
    /// Ortalama uzunluk en az 1 alınır: boş bir bilgi tabanında ya da yalnızca boş chunk'larda sıfıra bölme olmaz.
    /// </remarks>
    public Bm25Index(IReadOnlyList<IReadOnlyList<string>> documents, double k1 = 1.2, double b = 0.75)
    {
        _k1 = k1;
        _b = b;
        _lengths = new int[documents.Count];
        _termFrequencies = new Dictionary<string, int>[documents.Count];

        for (var index = 0; index < documents.Count; index++)
        {
            var frequencies = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var term in documents[index])
            {
                frequencies[term] = frequencies.GetValueOrDefault(term) + 1;
            }

            foreach (var term in frequencies.Keys)
            {
                _documentFrequencies[term] = _documentFrequencies.GetValueOrDefault(term) + 1;
            }

            _termFrequencies[index] = frequencies;
            _lengths[index] = documents[index].Count;
        }

        _averageLength = documents.Count == 0 ? 1 : Math.Max(1, _lengths.Average());
    }

    /// <summary>
    /// Sorguyla en az bir terimi paylaşan chunk'ları, en iyisi önce olacak şekilde BM25 puanıyla döndürür; her sonuç
    /// kapsama (coverage) değerini de taşır.
    /// </summary>
    /// <remarks>
    /// Sorgu terimleri tekilleştirilir: bir kelimeyi tekrarlamak ("iade iade") puanı şişirmez. Hiç ortak terimi olmayan
    /// chunk listeye girmez; böylece füzyona yalnızca gerçek sözcüksel adaylar gider. Eşit puanlarda chunk sırası
    /// kullanılır; sıralama deterministik olduğundan aynı soru her zaman aynı sonuçları verir.
    /// </remarks>
    public IReadOnlyList<LexicalMatch> Score(IReadOnlyList<string> queryTerms)
    {
        var terms = queryTerms.Distinct(StringComparer.Ordinal).ToArray();
        var matches = new List<LexicalMatch>();

        for (var index = 0; index < _termFrequencies.Length; index++)
        {
            var score = 0.0;

            foreach (var term in terms)
            {
                if (!_termFrequencies[index].TryGetValue(term, out var frequency))
                {
                    continue;
                }

                // Klasik BM25 terimi: idf · tf · (k1 + 1) / (tf + k1 · (1 − b + b · uzunluk / ortalama uzunluk)).
                var lengthNormalization = _k1 * (1 - _b + _b * _lengths[index] / _averageLength);
                score += InverseDocumentFrequency(term) * frequency * (_k1 + 1) / (frequency + lengthNormalization);
            }

            if (score > 0)
            {
                matches.Add(new LexicalMatch(index, score, Coverage(terms, index)));
            }
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.DocumentIndex)
            .ToList();
    }

    /// <summary>
    /// Sorgudaki bilginin chunk'ta bulunan payı, idf ile ağırlıklı: her sorgu terimi geçiyorsa 1.0, nadir (bilgi taşıyan)
    /// terimler eksikse düşük. Ham BM25 puanının aksine [0, 1] aralığıyla sınırlıdır; bu yüzden "yeterli kanıt var mı?"
    /// eşiğini (Kapı 1, varsayılan ≥ 0.5) besleyebilir.
    /// </summary>
    /// <remarks>
    /// Ham BM25 puanları sorgular arasında karşılaştırılamaz: terim sayısı, terimlerin idf değerleri ve chunk uzunluğuyla
    /// ölçeklenir; iki kelimelik bir soru tam eşleşmede bile altı kelimelik bir sorudan düşük puan alabilir, bu yüzden
    /// sabit bir puan eşiği anlamsız olurdu. Kapsama oranı ise her sorgu için kendi içinde normalize edilir. Korpusta hiç
    /// geçmeyen bir terim en yüksek idf'yi aldığından, bilgi tabanında karşılığı olmayan bir kelime içeren soru düşük
    /// kapsama alır ve dil modeli çağrılmadan reddedilebilir.
    /// </remarks>
    public double Coverage(IReadOnlyList<string> queryTerms, int documentIndex)
    {
        var total = 0.0;
        var found = 0.0;

        foreach (var term in queryTerms.Distinct(StringComparer.Ordinal))
        {
            var idf = InverseDocumentFrequency(term);
            total += idf;

            if (_termFrequencies[documentIndex].ContainsKey(term))
            {
                found += idf;
            }
        }

        return total == 0 ? 0 : found / total;
    }

    /// <summary>
    /// Lucene'in BM25 idf varyantı: ln(1 + (N − df + 0.5) / (df + 0.5)). Her zaman pozitiftir; korpusta hiç geçmeyen
    /// terimler en yüksek ağırlığı alır.
    /// </summary>
    /// <remarks>
    /// Klasik Robertson–Spärck Jones idf'si chunk'ların yarısından fazlasında geçen terimler için negatife düşer; o durumda
    /// yaygın bir kelimeyle eşleşmek puanı düşürür ve kapsama oranını bozardı. Logaritma içindeki "1 +" bunu önler.
    /// </remarks>
    private double InverseDocumentFrequency(string term)
    {
        var documentFrequency = _documentFrequencies.GetValueOrDefault(term);
        return Math.Log(1 + (_termFrequencies.Length - documentFrequency + 0.5) / (documentFrequency + 0.5));
    }
}
