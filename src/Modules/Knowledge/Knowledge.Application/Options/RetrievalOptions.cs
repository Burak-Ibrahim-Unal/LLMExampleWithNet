namespace Knowledge.Application.Options;

public sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    /// <summary>
    /// Chunks passed to the answer generator. Calibrated on the evaluation set: with 6, the second topic of a
    /// two-part question (delivery time + Wi-Fi) ranked 8th and was left out.
    /// </summary>
    public int TopK { get; set; } = 8;

    /// <summary>Candidates taken from each ranking (BM25 and vector) before fusion.</summary>
    public int CandidatePoolSize { get; set; } = 20;

    /// <summary>Reciprocal Rank Fusion constant.</summary>
    public int RrfK { get; set; } = 60;

    /// <summary>Gate 1: minimum best cosine similarity counted as evidence (hybrid mode).</summary>
    public double MinDenseScore { get; set; } = 0.55;

    /// <summary>Gate 1: minimum idf-weighted share of query terms found in one chunk.</summary>
    public double MinLexicalCoverage { get; set; } = 0.5;
}
