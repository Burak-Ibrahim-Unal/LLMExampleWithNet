using Knowledge.Domain.Entities;

namespace Knowledge.Application.Abstractions;

/// <summary>
/// Arama için kullanılan okuma modelinin portu: her bölümün bellek içi bir anlık görüntüsü (snapshot); ingest sonrasında
/// yeniden kurulur.
/// </summary>
/// <remarks>
/// Application yalnızca bu sözleşmeyi bilir; BM25, vektör benzerliği ve RRF birleşimi Infrastructure'daki
/// <c>KnowledgeIndex</c> içindedir. Böylece testler gerçek indeksi sahte bir embedder'la kullanabilir; korpus büyüdüğünde
/// de aynı arayüzün arkasına SQLite FTS5, pgvector veya Qdrant gibi bir çözüm handler'lar değişmeden konabilir.
/// </remarks>
public interface IKnowledgeIndex
{
    /// <summary>
    /// İndeks en az bir kez kurulduysa true. Kurulmadan gelen soru ve aramalar iş kuralıyla 503 alır; açılıştaki ingest
    /// başarısız olsa bile uygulama ayakta kalır ve reindex ile toparlanabilir.
    /// </summary>
    bool IsReady { get; }

    /// <summary>İndeksin durumu (doküman/bölüm sayısı, arama modu, kurulma zamanı); sağlık ucu ve ingest özeti bunu raporlar.</summary>
    IndexStatus Status { get; }

    /// <summary>
    /// Verilen dokümanlardan (bölümleriyle birlikte yüklenmiş olmalı) yeni bir anlık görüntü kurar ve eskisinin yerine koyar.
    /// </summary>
    /// <remarks>
    /// Değiştirme tek bir atamayla yapılır; eşzamanlı aramalar yarım kurulmuş bir indeks görmez. Saklanan vektörler ancak
    /// hepsi yapılandırılmış embedding modeliyle ve aynı boyutta üretilmişse kullanılır; aksi hâlde indeks BM25 moduna
    /// düşer, çünkü farklı modellerin vektörleri karşılaştırılamaz.
    /// </remarks>
    void Rebuild(IReadOnlyList<KnowledgeDocument> documents);

    /// <summary>Sorguyu terimlere ayırır ve hibrit modda bir kez embed eder; böylece birden çok arama aynı sorguyu yeniden kullanabilir.</summary>
    /// <remarks>
    /// Soru akışı aynı sorguyla ikinci kez arayabilir (yalnızca eski sürüm eşleştiğinde güncel sürümün bölümlerini
    /// getirmek için); embedding sunucusu uzak ve yavaş olduğundan vektör yalnızca bir kez hesaplanır. Embed etme
    /// başarısız olursa ya da vektörün boyutu indeksle uyuşmazsa vektör null bırakılır ve arama BM25 ile sürer.
    /// </remarks>
    Task<PreparedQuery> PrepareAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hazırlanmış sorguyla arar ve en iyi <paramref name="topK"/> sonucu Reciprocal Rank Fusion sırasıyla döndürür.
    /// İndeks kurulmadan çağrılmamalıdır; handler'lar önce <c>IsReady</c> kuralını denetler.
    /// </summary>
    /// <param name="query"><c>PrepareAsync</c> ile hazırlanmış sorgu; vektörü null ise yalnızca BM25 kullanılır.</param>
    /// <param name="topK">Döndürülecek en fazla sonuç sayısı.</param>
    /// <param name="filter">
    /// İsteğe bağlı bölüm süzgeci; soru akışı bunu, yalnızca eski sürüm eşleştiğinde güncel sürümün bölümleri arasında
    /// aramak için kullanır.
    /// </param>
    SearchResult Search(PreparedQuery query, int topK, Func<IndexedChunk, bool>? filter = null);

    /// <summary>Bir doküman ailesinin indekslenmiş tüm sürümleri, en eski yürürlük tarihi önce.</summary>
    /// <remarks>
    /// <c>VersionResolver</c> bu kataloğa bakarak, aramada hiç çıkmamış olsa bile ailenin yürürlükteki sürümünü bilir.
    /// Bilinmeyen bir aile veya henüz kurulmamış indeks için boş liste döner.
    /// </remarks>
    IReadOnlyList<DocumentVersion> GetDocumentVersions(string documentKey);
}

/// <summary>
/// Bir doküman sürümünün sürüm çözümü için gereken meta verisi (içeriksiz). <c>VersionResolver</c> hangi sürümün
/// yürürlükte olduğuna bu kayıtlara bakarak karar verir ve elenen sürümleri bunlarla raporlar.
/// </summary>
/// <param name="DocumentId">Sürüme özgü doküman kimliği (ör. "iade-politikasi-v2").</param>
/// <param name="DocumentKey">Doküman ailesi anahtarı; aynı prosedürün sürümleri bunu paylaşır.</param>
/// <param name="Title">Doküman başlığı; elenen sürümler raporlanırken kullanıcıya bununla gösterilir.</param>
/// <param name="Version">Sürüm numarası; yürürlük tarihleri eşitse sayısal olarak karşılaştırılır.</param>
/// <param name="EffectiveDate">Yürürlük tarihi; bugünden sonraysa sürüm henüz kullanılmaz.</param>
/// <param name="Status">active veya superseded; superseded sürüm hiç seçilmez.</param>
/// <param name="Category">Doküman türü (politika, prosedür, kılavuz, SSS); kaynaklar arası öncelik kuralında kullanılır.</param>
public sealed record DocumentVersion(
    string DocumentId,
    string DocumentKey,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    DocumentStatus Status,
    DocumentCategory Category);

/// <summary>
/// Aramanın hangi sinyallerle yapıldığı. API'de "lexical" / "hybrid" olarak görünür; değerlendirme aracı iki modu ayrı
/// ölçerek vektör aramanın katkısını gösterir.
/// </summary>
public enum RetrievalMode
{
    /// <summary>Yalnızca BM25 (embedding ucu yok, saklanan vektörler kullanılamıyor ya da sorgu embed edilemedi).</summary>
    Lexical,

    /// <summary>BM25 ve vektör benzerliği, Reciprocal Rank Fusion ile birleştirilir.</summary>
    Hybrid
}

/// <summary>İndeksin anlık durumu; sağlık ucu ve ingest özeti bunu raporlar.</summary>
/// <param name="IsReady">İndeks en az bir kez kurulduysa true.</param>
/// <param name="DocumentCount">İndeksteki doküman sürümü sayısı.</param>
/// <param name="ChunkCount">İndeksteki bölüm (chunk) sayısı.</param>
/// <param name="Mode">Saklanan vektörler kullanılabiliyorsa <c>Hybrid</c>, aksi hâlde <c>Lexical</c>.</param>
/// <param name="BuiltAtUtc">Son kurulma zamanı (UTC); indeks henüz kurulmadıysa null.</param>
public sealed record IndexStatus(bool IsReady, int DocumentCount, int ChunkCount, RetrievalMode Mode, DateTime? BuiltAtUtc);

/// <summary>İndeksteki tek bir bölüm; ait olduğu doküman sürümünün meta verisi bölüme kopyalanmıştır.</summary>
/// <remarks>
/// Meta verinin (sürüm, yürürlük tarihi, durum, tür) her bölümde taşınması, sürüm çözümünün, öncelik kuralının ve prompt
/// başlığının ek bir veritabanı sorgusu olmadan doğrudan bellekteki anlık görüntüden çalışmasını sağlar.
/// </remarks>
/// <param name="ChunkId">Bölümün kalıcı kimliği (veritabanındaki <c>DocumentChunk</c> kimliği).</param>
/// <param name="DocumentId">Bölümün ait olduğu doküman sürümünün kimliği.</param>
/// <param name="DocumentKey">Doküman ailesi anahtarı.</param>
/// <param name="Title">Doküman başlığı; prompt'taki bölüm başlığında ve kaynak listesinde gösterilir.</param>
/// <param name="Version">Doküman sürümü; prompt başlığında modele ve yanıtta kullanıcıya gösterilir.</param>
/// <param name="EffectiveDate">Doküman sürümünün yürürlük tarihi; sürüm çözümü ve öncelik kuralının eşitlik bozucusu.</param>
/// <param name="Status">Doküman sürümünün durumu (active/superseded); superseded sürümün bölümleri modele verilmez.</param>
/// <param name="Category">Doküman türü; kaynaklar arası çelişkide hangi kaynağın üstün geldiğini belirler.</param>
/// <param name="SectionPath">Başlık yolu; atıflarda gösterilir.</param>
/// <param name="Content">Bölüm metni; modele verilen ve alıntıların doğrulandığı metin budur.</param>
public sealed record IndexedChunk(
    Guid ChunkId,
    string DocumentId,
    string DocumentKey,
    string Title,
    string Version,
    DateOnly EffectiveDate,
    DocumentStatus Status,
    DocumentCategory Category,
    string SectionPath,
    string Content);

/// <summary>Aramaya hazırlanmış sorgu: özgün metin, BM25 terimleri ve (varsa) sorgu vektörü.</summary>
/// <param name="Text">Kullanıcının yazdığı sorgu.</param>
/// <param name="Terms">Normalleştirilmiş, stopword'leri atılmış ve köklenmiş (F5) BM25 terimleri.</param>
/// <param name="Vector">
/// Sorgu embedding'i; indeks lexical moddaysa, embedding başarısız olduysa ya da boyut uyuşmadıysa null. Arama ucu
/// <c>mode=lexical</c> isteğinde bunu bilerek null yaparak BM25-yalnız aramayı zorlar.
/// </param>
public sealed record PreparedQuery(string Text, IReadOnlyList<string> Terms, float[]? Vector);

/// <summary>Tek bir arama sonucu; sıralamayı belirleyen füzyon skoru ve teşhis için ayrı ayrı sinyaller.</summary>
/// <remarks>
/// Sıralama yalnızca RRF skoruyla yapılır, çünkü BM25 ve kosinüs birbiriyle ilgisiz ölçeklerdedir. Ham skorlar yine de
/// taşınır: arama ucu ve yanıt teşhisleri bir bölümün hangi sinyalle bulunduğunu gösterebilsin.
/// </remarks>
/// <param name="Chunk">Bulunan bölüm.</param>
/// <param name="FusedScore">Sıralamada kullanılan Reciprocal Rank Fusion skoru.</param>
/// <param name="LexicalScore">BM25 skoru (bölüm sorguyla hiç terim paylaşmıyorsa 0).</param>
/// <param name="LexicalCoverage">Sorgu terimlerinin bölümde bulunan, idf ağırlıklı payı (0..1).</param>
/// <param name="DenseScore">Kosinüs benzerliği; lexical modda null.</param>
public sealed record SearchHit(IndexedChunk Chunk, double FusedScore, double LexicalScore, double LexicalCoverage, double? DenseScore);

/// <summary>Bir aramanın sonucu: sıralı isabetler ve Kapı 1'in (<c>AnswerabilityPolicy</c>) kullandığı en iyi kanıt sinyalleri.</summary>
/// <remarks>
/// En iyi değerler yalnızca döndürülen isabetler üzerinden değil, süzgece uyan tüm adaylar üzerinden hesaplanır; böylece
/// Kapı 1 kararı istenen sonuç sayısına bağlı olmaz.
/// </remarks>
/// <param name="Mode">Bu aramada fiilen kullanılan mod.</param>
/// <param name="Hits">RRF sırasına göre isabetler.</param>
/// <param name="MaxDenseScore">Tüm adaylar arasındaki en iyi kosinüs benzerliği (lexical modda 0).</param>
/// <param name="MaxLexicalCoverage">Tüm adaylar arasındaki en iyi kelime kapsamı (0..1).</param>
public sealed record SearchResult(RetrievalMode Mode, IReadOnlyList<SearchHit> Hits, double MaxDenseScore, double MaxLexicalCoverage);
