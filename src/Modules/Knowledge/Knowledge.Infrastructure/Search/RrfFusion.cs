namespace Knowledge.Infrastructure.Search;

/// <summary>
/// Reciprocal Rank Fusion: score(item) = Σ 1 / (k + rank). It merges rankings without having to
/// calibrate BM25 scores against cosine similarities, which live on unrelated scales.
/// </summary>
public static class RrfFusion
{
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
