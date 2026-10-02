namespace Knowledge.Application.Options;

/// <summary>
/// Arama ve Kapı 1 ayarları; <c>Retrieval</c> yapılandırma bölümünden bağlanır (ör. <c>Retrieval__TopK</c> ortam değişkeni).
/// </summary>
/// <remarks>
/// Varsayılanlar 16 soruluk değerlendirme setiyle seçildi. Değerlerin koddan değil yapılandırmadan gelmesi, farklı bir
/// embedding modeline ya da korpusa geçildiğinde yeniden derlemeden kalibrasyon yapılabilmesini sağlar (kosinüs
/// değerlerinin dağılımı modelden modele değişir). Set küçük olduğu için yeni sorularda eşiklerin gözden geçirilmesi gerekebilir.
/// </remarks>
public sealed class RetrievalOptions
{
    /// <summary>Yapılandırmadaki bölüm adı.</summary>
    public const string SectionName = "Retrieval";

    /// <summary>
    /// Yanıt üreticisine verilen bölüm sayısı (varsayılan 8). Değerlendirme setiyle ayarlandı: 6 iken iki parçalı bir
    /// sorunun (teslimat süresi + Wi-Fi) ikinci konusunun bölümü 8. sıraya düştü ve bağlam dışında kaldı.
    /// </summary>
    /// <remarks>
    /// Soru akışı, sürüm çözümünde elenecek eski sürümlere yer bırakmak için önce bunun iki katı kadar aday alır. Arama
    /// ucunda <c>topK</c> verilmezse varsayılan da bu değerdir. Daha büyük değer çok konulu sorulara yardım eder ama
    /// prompt'u uzatır ve modele daha fazla ilgisiz bölüm gösterir.
    /// </remarks>
    public int TopK { get; set; } = 8;

    /// <summary>Füzyondan önce her sıralamadan (BM25 ve vektör) alınan aday sayısı (varsayılan 20).</summary>
    /// <remarks>
    /// Füzyona her sıralamanın yalnızca bu kadarlık baş kısmı girer. 20, soru akışının istediği aday sayısını
    /// (<c>TopK</c> × 2 = 16) ve arama ucunun en fazla 20 sonucunu karşılayacak büyüklüktedir.
    /// </remarks>
    public int CandidatePoolSize { get; set; } = 20;

    /// <summary>Reciprocal Rank Fusion sabiti k (varsayılan 60): skor = Σ 1 / (k + sıra).</summary>
    /// <remarks>
    /// 60, RRF'nin özgün çalışmasında önerilen ve yaygın kullanılan değerdir. Büyük k sıralar arasındaki farkı yumuşatır;
    /// böylece yalnızca tek bir sıralamada birinci olan bölüm, iki sıralamada da üst sıralarda olan bölümü kolayca geçemez.
    /// RRF skor değil sıra kullandığı için BM25 ile kosinüs arasında ölçek kalibrasyonu gerekmez.
    /// </remarks>
    public int RrfK { get; set; } = 60;

    /// <summary>Kapı 1: hibrit modda kanıt sayılan en düşük "en iyi kosinüs benzerliği" (varsayılan 0.55).</summary>
    /// <remarks>
    /// Değerlendirme setinde yanıtlanabilir sorulardaki en düşük en-iyi kosinüs 0.60, Kapı 1'de reddedilen alan dışı
    /// sorularda ise 0.45–0.46 idi; 0.55 iki grubun arasında kalır. Alana yakın cevapsız sorular (0.61–0.62) benzerlikle
    /// ayrılamaz; onları Kapı 2'de model reddeder. Değer bge-m3 ile ölçüldü; başka bir embedding modelinde yeniden kalibre
    /// edilmelidir.
    /// </remarks>
    public double MinDenseScore { get; set; } = 0.55;

    /// <summary>Kapı 1: sorgu terimlerinin tek bir bölümde bulunması gereken en düşük idf ağırlıklı payı (varsayılan 0.5).</summary>
    /// <remarks>
    /// Ham BM25 skoru sorgudan sorguya karşılaştırılamadığı için eşik 0..1 aralığındaki kapsama dayanır. 0.5, sorgunun bilgi
    /// taşıyan terimlerinin (idf ağırlığıyla) en az yarısının aynı bölümde geçmesini ister. Vektör sinyalinin zayıf kaldığı,
    /// Türkçe karakter kullanılmadan yazılmış sorular bu yoldan geçer; embedding kapalıyken Kapı 1'in tek sinyali budur.
    /// </remarks>
    public double MinLexicalCoverage { get; set; } = 0.5;
}
