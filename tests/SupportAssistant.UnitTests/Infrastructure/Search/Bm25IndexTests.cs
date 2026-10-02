using Knowledge.Infrastructure.Search;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Search;

/// <summary>
/// <see cref="Bm25Index"/> birim testleri. İndeks, önceden token'lara ayrılmış (normalize edilmiş ve F5 kökü alınmış)
/// küçük, elle kurulmuş bir korpus üzerinde sınanır; böylece sıralama ve kapsama (coverage) değerleri elle
/// hesaplanabilir ve tokenizer'dan bağımsız doğrulanır. Kapsama metriği Kapı 1'in "yeterli kanıt var mı?" kararını
/// beslediği için bu testler dolaylı olarak ret/yanıt davranışını da korur.
/// </summary>
public sealed class Bm25IndexTests
{
    /// <summary>
    /// Önceden token'lara ayrılmış örnek korpus: 0 = iade süresi, 1 = kargo ücreti, 2 = iade kargo ücreti. Terimler
    /// <c>SearchTokenizer</c>'ın üreteceği biçimdedir ("süresi" → "sures", "ücret" → "ucret", "Lumora" → "lumor").
    /// </summary>
    private static readonly IReadOnlyList<IReadOnlyList<string>> Documents =
    [
        ["iade", "sures", "30", "gun"],
        ["kargo", "ucret", "750", "tl"],
        ["iade", "kargo", "ucret", "lumor"]
    ];

    /// <summary>
    /// Sorgunun tüm terimlerini içeren dokümanın ilk sırada döndüğünü doğrular: "iade kargo" için iki terimi de içeren
    /// 2 numaralı doküman, "iade sures" için 0 numaralı doküman. BM25 hibrit aramanın her zaman açık olan ayağıdır ve
    /// embedding sunucusu yokken tek sıralamadır; temel sıralama bozulursa modele giden bağlama yanlış bölümler girer.
    /// </summary>
    [Fact]
    public void Score_ranks_the_document_containing_every_query_term_first()
    {
        var index = new Bm25Index(Documents);

        index.Score(["iade", "kargo"])[0].DocumentIndex.ShouldBe(2);
        index.Score(["iade", "sures"])[0].DocumentIndex.ShouldBe(0);
    }

    /// <summary>
    /// Yalnızca sorguyla en az bir terim paylaşan dokümanların döndüğünü doğrular: korpusta hiç geçmeyen "homek" için
    /// sonuç boştur, "sures" için yalnızca 0 numaralı doküman gelir.
    /// </summary>
    /// <remarks>
    /// Sıfır skorlu dokümanlar listeye girseydi RRF onlara da sıraya dayalı pozitif bir skor verir ve sorguyla hiçbir
    /// ortak sözcüğü olmayan bölümler bağlama taşınırdı.
    /// </remarks>
    [Fact]
    public void Score_returns_only_documents_sharing_a_term_with_the_query()
    {
        var index = new Bm25Index(Documents);

        index.Score(["homek"]).ShouldBeEmpty();
        index.Score(["sures"]).Select(match => match.DocumentIndex).ShouldBe([0]);
    }

    /// <summary>
    /// Kapsamanın, sorgu terimlerinin dokümanda bulunan kısmının idf ağırlıklı payı olduğunu doğrular: tüm terimler
    /// dokümanda varsa 1.0; yaygın "iade" bulunup korpusta hiç geçmeyen (dolayısıyla en yüksek idf'e sahip) "homek"
    /// bulunmadığında yaklaşık 0.18.
    /// </summary>
    /// <remarks>
    /// Ham BM25 skorları sorgudan sorguya karşılaştırılamadığı için Kapı 1 sabit eşiğini (≥ 0.5) bu [0, 1] aralıklı
    /// metriğe uygular. Alan dışı bir soru yaygın bir sözcükle eşleşse bile, bilgi taşıyan nadir terim eksik olduğundan
    /// kapsaması düşük kalır ve dil modeli hiç çağrılmadan reddedilir. Beklenen değerler Lucene'in BM25 idf formülüyle
    /// elle hesaplanmıştır.
    /// </remarks>
    [Fact]
    public void Coverage_is_the_idf_weighted_share_of_query_terms_found_in_the_document()
    {
        var index = new Bm25Index(Documents);

        index.Coverage(["iade", "sures"], 0).ShouldBe(1.0, 1e-9);

        // idf(iade) = ln(1 + 1.5 / 2.5) = 0.470004; korpusta hiç geçmeyen terim: idf(homek) = ln(1 + 3.5 / 0.5) = 2.079442
        // kapsama = 0.470004 / (0.470004 + 2.079442) = 0.184355
        index.Coverage(["iade", "homek"], 0).ShouldBe(0.184355, 1e-5);
    }
}
