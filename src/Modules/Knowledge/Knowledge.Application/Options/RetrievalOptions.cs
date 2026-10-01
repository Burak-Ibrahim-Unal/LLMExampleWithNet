namespace Knowledge.Application.Options;

public sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    /// <summary>Chunks passed to the answer generator.</summary>
    public int TopK { get; set; } = 6;

    /// <summary>Candidates taken from each ranking (BM25 and vector) before fusion.</summary>
    public int CandidatePoolSize { get; set; } = 20;

    /// <summary>Reciprocal Rank Fusion constant.</summary>
    public int RrfK { get; set; } = 60;

    /// <summary>Gate 1: minimum best cosine similarity counted as evidence (hybrid mode).</summary>
    public double MinDenseScore { get; set; } = 0.55;

    /// <summary>Gate 1: minimum idf-weighted share of query terms found in one chunk.</summary>
    public double MinLexicalCoverage { get; set; } = 0.5;
}
