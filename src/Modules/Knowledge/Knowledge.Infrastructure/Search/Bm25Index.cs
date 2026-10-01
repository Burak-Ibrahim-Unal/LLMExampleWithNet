namespace Knowledge.Infrastructure.Search;

public readonly record struct LexicalMatch(int DocumentIndex, double Score, double Coverage);

/// <summary>
/// Okapi BM25 over pre-tokenized documents (here: knowledge base chunks).
/// </summary>
public sealed class Bm25Index
{
    private readonly double _k1;
    private readonly double _b;
    private readonly double _averageLength;
    private readonly int[] _lengths;
    private readonly Dictionary<string, int>[] _termFrequencies;
    private readonly Dictionary<string, int> _documentFrequencies = new(StringComparer.Ordinal);

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

    /// <summary>Documents that share at least one term with the query, best first.</summary>
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
    /// Share of the query's information found in the document, weighted by idf: 1.0 when every query
    /// term occurs, low when the rare (informative) terms are missing. Unlike the raw BM25 score it is
    /// bounded to [0, 1], so it can drive the "enough evidence?" threshold.
    /// </summary>
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

    // Lucene's BM25 idf variant: always positive; terms unseen in the corpus get the highest weight.
    private double InverseDocumentFrequency(string term)
    {
        var documentFrequency = _documentFrequencies.GetValueOrDefault(term);
        return Math.Log(1 + (_termFrequencies.Length - documentFrequency + 0.5) / (documentFrequency + 0.5));
    }
}
