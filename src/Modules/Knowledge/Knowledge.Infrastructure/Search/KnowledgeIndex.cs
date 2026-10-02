using Knowledge.Application.Abstractions;
using Knowledge.Application.Options;
using Knowledge.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Search;

/// <summary>
/// Bütün chunk'lar üzerinde bellek içi hibrit indeks: BM25 (her zaman) ve embedding kosinüs benzerliği (varsa), Reciprocal
/// Rank Fusion ile birleştirilir. Yeniden kurulum, öncekinin yerini tek bir atamayla alan değişmez (immutable) bir
/// snapshot üretir; böylece eşzamanlı aramalar asla yarım kurulmuş bir indeks görmez.
/// </summary>
/// <remarks>
/// Kaba kuvvet (brute force) bilinçli bir tercihtir: bilgi tabanı birkaç düzine chunk içerir, bu yüzden bir vektör
/// veritabanı ölçülebilir bir kazanç getirmeden operasyonel yük eklerdi. <see cref="IKnowledgeIndex"/> portu, gerektiğinde
/// bu parçanın (ör. SQLite FTS5, pgvector veya Qdrant ile) değiştirilmesini mümkün kılar. Singleton olarak kaydedilir;
/// snapshot, <c>VersionResolver</c>'ın kullandığı doküman ailesi başına sürüm kataloğunu da tutar.
/// </remarks>
/// <param name="embedder">Sorguları vektöre çevirir; <c>ModelName</c> saklanan vektörlerin kullanılabilirliğini belirler.</param>
/// <param name="options">Aday havuzu boyutu ve RRF sabiti (<see cref="RetrievalOptions"/>).</param>
/// <param name="logger">BM25'e geri düşülen durumları (embedding hatası, boyut uyuşmazlığı) uyarı olarak kaydeder.</param>
public sealed class KnowledgeIndex(
    ITextEmbedder embedder,
    IOptions<RetrievalOptions> options,
    ILogger<KnowledgeIndex> logger) : IKnowledgeIndex
{
    /// <summary>
    /// Güncel snapshot; henüz kurulmadıysa <c>null</c>. <c>volatile</c>, yeni atanan snapshot'ın kilitsiz okuyan bütün
    /// thread'lerce gecikmeden ve tam kurulmuş hâliyle görülmesini sağlar. Okuyan her üye alanı bir kez okur (gerekirse
    /// yerel değişkene alır) ve işlem boyunca tutarlı tek bir snapshot ile çalışır.
    /// </summary>
    private volatile Snapshot? _snapshot;

    /// <summary>
    /// İlk başarılı yeniden kurulumdan sonra <c>true</c> olur. Soru ve arama akışları önce bunu
    /// (<c>CheckIndexReady</c> iş kuralı) kontrol eder; indeks hazır değilse 503 döner.
    /// </summary>
    public bool IsReady => _snapshot is not null;

    /// <summary>
    /// İndeksin anlık durumu: hazır mı, kaç doküman ve chunk var, arama modu (lexical/hybrid) ve kurulma zamanı. Sağlık
    /// uç noktası (<c>GET /v1/health</c>) ve ingestion özeti bunu raporlar.
    /// </summary>
    public IndexStatus Status
    {
        get
        {
            var snapshot = _snapshot;

            return snapshot is null
                ? new IndexStatus(false, 0, 0, RetrievalMode.Lexical, null)
                : new IndexStatus(true, snapshot.DocumentCount, snapshot.Chunks.Length, snapshot.Mode, snapshot.BuiltAtUtc);
        }
    }

    /// <summary>
    /// Verilen dokümanlardan yeni bir snapshot kurar ve tek atamayla yayınlar: chunk'ları okuma modeline çevirir, BM25
    /// indeksini kurar, saklanan vektörlerin kullanılabilir olup olmadığına karar verir ve sürüm kataloğunu oluşturur.
    /// </summary>
    /// <remarks>
    /// Chunk'lar doküman sırasına ve her doküman içinde bölüm sırasına göre dizilir; aynı konum chunk dizisinde, BM25
    /// listesinde ve vektör dizisinde aynı chunk'ı gösterir — aramanın tamamı bu değişmeze dayanır.
    /// <see cref="IndexedChunk"/> doküman metadata'sını (sürüm, yürürlük tarihi, durum, kategori) chunk'la birlikte taşır;
    /// böylece sürüm çözümü ve kaynak önceliği veritabanına gitmeden çalışır. Vektörler "ya hep ya hiç" kuralıyla
    /// kullanılır: tek bir chunk'ın vektörü eksikse ya da başka bir modelden geliyorsa yoğun (dense) arama tamamen kapanır
    /// ve BM25'e düşülür. Kısmi vektörlerle aramak, vektörü olmayan chunk'ları füzyonda sistematik olarak geriye iterdi; bir
    /// sonraki yeniden indeksleme eksik vektörleri tamamlar.
    /// </remarks>
    public void Rebuild(IReadOnlyList<KnowledgeDocument> documents)
    {
        var entries = documents
            .SelectMany(document => document.Chunks.OrderBy(chunk => chunk.Order).Select(chunk => (Document: document, Chunk: chunk)))
            .ToList();

        var chunks = entries
            .Select(entry => new IndexedChunk(
                entry.Chunk.Id,
                entry.Document.SourceId,
                entry.Document.DocumentKey,
                entry.Document.Title,
                entry.Document.Version,
                entry.Document.EffectiveDate,
                entry.Document.Status,
                entry.Document.Category,
                entry.Chunk.SectionPath,
                entry.Chunk.Content))
            .ToArray();

        // Doküman başlığı ve bölüm başlığındaki kelimeler de bölüm metninin parçası sayılır: bir soru için en iyi eşleşme
        // çoğu zaman "İade Süresi" gibi bir başlıktır ve içerik bu kelimeleri tekrar etmeyebilir.
        var lexical = new Bm25Index(entries
            .Select(entry => SearchTokenizer.Tokenize($"{entry.Document.Title} {entry.Chunk.SectionPath} {entry.Chunk.Content}"))
            .ToList());

        // Vektörler ancak hepsini aynı model ürettiyse sorgu vektörleriyle karşılaştırılabilir. Ayrıca hepsinin boyutu aynı
        // olmalıdır: aynı adla farklı bir model çalıştırıldıysa boyutlar karışabilir. Koşullardan biri sağlanmazsa yoğun
        // arama kapatılır ve indeks yalnızca BM25 ile çalışır.
        var vectorsUsable = embedder.IsEnabled
            && entries.Count > 0
            && entries.All(entry => entry.Chunk.Embedding is not null && entry.Chunk.EmbeddingModel == embedder.ModelName)
            && entries.Select(entry => entry.Chunk.Embedding!.Length).Distinct().Count() == 1;

        // Sürüm kataloğu: her doküman ailesinin (DocumentKey) sürümleri, en eski yürürlük tarihi önce. VersionResolver
        // aramada eşleşmeyen sürümleri de buradan görür; örneğin yalnızca eski sürüm eşleştiğinde güncel sürümü seçebilir.
        var versions = documents
            .GroupBy(document => document.DocumentKey, StringComparer.Ordinal)
            .ToDictionary(
                family => family.Key,
                family => (IReadOnlyList<DocumentVersion>)family
                    .OrderBy(document => document.EffectiveDate)
                    .Select(document => new DocumentVersion(document.SourceId, document.DocumentKey, document.Title, document.Version, document.EffectiveDate, document.Status, document.Category))
                    .ToList(),
                StringComparer.Ordinal);

        // Tek atama: okuyucular ya eski snapshot'ı ya da tamamen kurulmuş yenisini görür, ara bir hâl görmez.
        _snapshot = new Snapshot(
            chunks,
            lexical,
            vectorsUsable ? entries.Select(entry => entry.Chunk.Embedding!).ToArray() : null,
            versions,
            documents.Count,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Sorguyu bir kez hazırlar: terimlere ayırır ve indeks hibrit moddaysa sorgunun embedding'ini alır. Aynı
    /// <see cref="PreparedQuery"/> birden çok aramada yeniden kullanılır (ör. soru akışındaki güncel sürümün bölümlerini
    /// getiren ek aramalar); uzak embedding sunucusuna soru başına bir kez gidilir.
    /// </summary>
    /// <remarks>
    /// Embedding yalnızca snapshot'ta kullanılabilir vektörler varsa istenir; aksi hâlde sunucuya boşuna gidilmez. Sorgu
    /// embedding'i başarısız olursa (sunucuya ulaşılamaması ya da yanıt vermeyen sunucunun zaman aşımı) uyarı loglanır ve
    /// arama BM25 ile sürer: embedding sunucusunun kesintisi soruları düşürmez. Yalnızca çağıranın kendi iptali (belirteci
    /// iptal edilmiş <c>OperationCanceledException</c>) yutulmaz, çağırana yayılır; zaman aşımı da
    /// <c>OperationCanceledException</c> olarak gelir, bu yüzden ikisi belirtecin durumuna bakılarak ayrılır. Sorgu vektörünün boyutu
    /// indekstekinden farklıysa (yapılandırılmış ad altında başka bir model yanıt veriyorsa) vektör uyarıyla atılır; bu
    /// kontrol eklenmeden önce boyut uyuşmazlığı bütün benzerlikleri 0 yapıyor ve anlamsız bir vektör sıralaması füzyona
    /// karışıyordu.
    /// </remarks>
    public async Task<PreparedQuery> PrepareAsync(string query, CancellationToken cancellationToken = default)
    {
        var terms = SearchTokenizer.Tokenize(query);
        var indexedVectors = _snapshot?.Vectors;
        float[]? vector = null;

        if (indexedVectors is not null)
        {
            try
            {
                vector = await embedder.EmbedQueryAsync(query, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Zaman aşımı da buraya düşer: HTTP istemcileri onu OperationCanceledException olarak bildirir, ama
                // çağıran iptal etmediği için bu bir sunucu arızasıdır ve arama BM25 ile sürer.
                logger.LogWarning(exception, "Query embedding failed; searching with BM25 only.");
            }

            // Yapılandırılmış ad altında farklı bir model yanıt veriyor; onun vektörleri indekstekilerle karşılaştırılamaz.
            if (vector is not null && vector.Length != indexedVectors[0].Length)
            {
                logger.LogWarning(
                    "The query embedding has {QueryDimensions} dimensions but the index has {IndexDimensions}; searching with BM25 only. " +
                    "If the embedding model changed, change Embeddings:Model (or delete the database) and reindex.",
                    vector.Length,
                    indexedVectors[0].Length);
                vector = null;
            }
        }

        return new PreparedQuery(query, terms, vector);
    }

    /// <summary>
    /// Hazırlanmış sorguyla arar: BM25 sıralaması ve (sorgu vektörü varsa) kosinüs sıralaması ayrı ayrı çıkarılır, her
    /// birinden en iyi <c>CandidatePoolSize</c> aday RRF ile birleştirilir ve ilk <paramref name="topK"/> isabet döner.
    /// </summary>
    /// <param name="query"><see cref="PrepareAsync"/> çıktısı.</param>
    /// <param name="topK">Birleştirmeden sonra döndürülecek isabet sayısı.</param>
    /// <param name="filter">
    /// İsteğe bağlı chunk filtresi. Soru akışı, yalnızca eski sürüm eşleştiğinde güncel sürümün en iyi bölümlerini
    /// getirmek için aramayı tek bir dokümanla sınırlamakta kullanır.
    /// </param>
    /// <remarks>
    /// Aday havuzu (varsayılan 20) ile <paramref name="topK"/> farklı şeylerdir: havuz, füzyona her listeden kaç adayın
    /// gireceğini belirler; soru akışı sürüm çözümünde elenecekleri hesaba katarak TopK'nın iki katını ister ve modele en
    /// fazla TopK (8) chunk gönderir. Kapı 1'in girdileri — en iyi kosinüs (<c>MaxDenseScore</c>) ve en iyi kapsama
    /// (<c>MaxLexicalCoverage</c>) — yalnızca döndürülen isabetler değil, filtreyi geçen bütün adaylar üzerinden hesaplanır.
    /// Sonucun modu, yoğun arama bu sorgu için gerçekten çalıştıysa <c>Hybrid</c>'dir; sorgu embedding'i alınamadıysa
    /// indeks hibrit olsa bile <c>Lexical</c> raporlanır.
    /// </remarks>
    public SearchResult Search(PreparedQuery query, int topK, Func<IndexedChunk, bool>? filter = null)
    {
        // Snapshot bir kez okunur: arama sürerken yeniden indeksleme olsa bile bu çağrı tutarlı tek bir snapshot ile biter.
        // Kurulmamış indeksle buraya gelinmesi programlama hatasıdır; çağıranlar önce IsReady'yi kontrol eder.
        var snapshot = _snapshot ?? throw new InvalidOperationException("The knowledge index has not been built yet.");
        var settings = options.Value;

        // Aynı filtre hem BM25 hem vektör adaylarına uygulanır; filtre verilmemişse her chunk geçer.
        bool IsAllowed(int chunkIndex) => filter is null || filter(snapshot.Chunks[chunkIndex]);

        // Bütün sözcüksel eşleşmeler (havuzla sınırlanmadan) saklanır: vektör listesinden gelen bir isabet için de gerçek
        // BM25 puanı ve kapsama raporlanabilsin.
        var lexicalMatches = snapshot.Lexical.Score(query.Terms).Where(match => IsAllowed(match.DocumentIndex)).ToList();
        var lexicalByChunk = lexicalMatches.ToDictionary(match => match.DocumentIndex);
        var rankings = new List<IReadOnlyList<int>>
        {
            lexicalMatches.Take(settings.CandidatePoolSize).Select(match => match.DocumentIndex).ToList()
        };

        Dictionary<int, double>? denseScores = null;

        if (query.Vector is not null && snapshot.Vectors is not null)
        {
            // Kaba kuvvet: filtreyi geçen her chunk için kosinüs hesaplanır; birkaç düzine chunk için maliyet ihmal edilebilir.
            denseScores = Enumerable.Range(0, snapshot.Chunks.Length)
                .Where(IsAllowed)
                .ToDictionary(chunkIndex => chunkIndex, chunkIndex => CosineSimilarity(query.Vector, snapshot.Vectors[chunkIndex]));

            rankings.Add(denseScores
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key)
                .Take(settings.CandidatePoolSize)
                .Select(entry => entry.Key)
                .ToList());
        }

        var hits = RrfFusion.Fuse(rankings, settings.RrfK)
            .Take(topK)
            .Select(fused =>
            {
                // Sözcüksel eşleşmesi olmayan (yalnızca vektör listesinden gelen) chunk'ta lexical varsayılan değerdedir:
                // Score = 0, Coverage = 0.
                lexicalByChunk.TryGetValue(fused.Item, out var lexical);
                return new SearchHit(snapshot.Chunks[fused.Item], fused.Score, lexical.Score, lexical.Coverage, denseScores?[fused.Item]);
            })
            .ToList();

        return new SearchResult(
            denseScores is null ? RetrievalMode.Lexical : RetrievalMode.Hybrid,
            hits,
            MaxDenseScore: denseScores is { Count: > 0 } ? denseScores.Values.Max() : 0,
            MaxLexicalCoverage: lexicalMatches.Count > 0 ? lexicalMatches.Max(match => match.Coverage) : 0);
    }

    /// <summary>
    /// Bir doküman ailesinin indekslenmiş bütün sürümlerini, en eski yürürlük tarihi önce olacak şekilde döndürür; aile
    /// bilinmiyorsa veya indeks henüz kurulmadıysa boş liste döner.
    /// </summary>
    /// <remarks>
    /// <c>VersionResolver</c> bu kataloğu kullanarak aday listesinde hiç görünmeyen sürümleri de bilir: yalnızca eski sürüm
    /// eşleştiğinde güncel sürümü seçip onun bölümlerini yerine koydurabilir. Arama ile bu çağrı arasında indeks yeniden
    /// kurulursa aile bulunamayabilir; <c>VersionResolver</c> o durumda isabetlerdeki metadata'ya düşer.
    /// </remarks>
    public IReadOnlyList<DocumentVersion> GetDocumentVersions(string documentKey)
    {
        return _snapshot?.Versions.GetValueOrDefault(documentKey) ?? [];
    }

    /// <summary>
    /// İki vektörün kosinüs benzerliğini hesaplar; boyutlar farklıysa veya vektörlerden biri sıfır vektörse 0 döner.
    /// </summary>
    /// <remarks>
    /// Embedder vektörleri birim uzunluğa ölçeklediğinden kosinüs burada nokta çarpımına eşittir; yine de normlar
    /// hesaplanır, böylece birim uzunlukta olmayan vektörlerde (başka bir sağlayıcı, test verisi) de sonuç doğru kalır.
    /// Boyut kontrolü savunma amaçlıdır: <see cref="PrepareAsync"/> ile <c>Search</c> arasında indeks farklı
    /// boyutlu vektörlerle yeniden kurulursa istisna yerine 0 döner. Sıfır norm kontrolü 0'a bölmeyi (NaN) önler;
    /// toplamlar <c>double</c>'da biriktirilir.
    /// </remarks>
    private static double CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            return 0;
        }

        double dot = 0, leftNorm = 0, rightNorm = 0;

        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        return leftNorm == 0 || rightNorm == 0 ? 0 : dot / Math.Sqrt(leftNorm * rightNorm);
    }

    /// <summary>
    /// Bir yeniden kurulumun değişmez sonucu. Arama için gereken her şeyi tek bir nesnede topladığı için tek atamayla
    /// yayınlanabilir ve okuyucular kilitsiz çalışır.
    /// </summary>
    /// <param name="Chunks">Okuma modeline çevrilmiş chunk'lar; konumları BM25 ve vektör dizileriyle ortak anahtardır.</param>
    /// <param name="Lexical">Aynı sıradaki chunk'lar üzerine kurulmuş BM25 indeksi.</param>
    /// <param name="Vectors">Chunk vektörleri, aynı sırada; kullanılabilir değilse <c>null</c> (yalnızca BM25).</param>
    /// <param name="Versions">Doküman ailesi → sürümler (en eski yürürlük tarihi önce).</param>
    /// <param name="DocumentCount">İndekslenen doküman (sürüm) sayısı.</param>
    /// <param name="BuiltAtUtc">Snapshot'ın kurulduğu an (UTC); sağlık uç noktasında gösterilir.</param>
    private sealed record Snapshot(
        IndexedChunk[] Chunks,
        Bm25Index Lexical,
        float[][]? Vectors,
        IReadOnlyDictionary<string, IReadOnlyList<DocumentVersion>> Versions,
        int DocumentCount,
        DateTime BuiltAtUtc)
    {
        /// <summary>Vektörler kullanılabiliyorsa <c>Hybrid</c>, aksi hâlde <c>Lexical</c>.</summary>
        public RetrievalMode Mode => Vectors is null ? RetrievalMode.Lexical : RetrievalMode.Hybrid;
    }
}
