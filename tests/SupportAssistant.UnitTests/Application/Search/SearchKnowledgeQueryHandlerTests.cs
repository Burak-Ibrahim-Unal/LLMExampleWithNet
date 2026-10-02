using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Application.Options;
using Knowledge.Application.Queries.SearchKnowledge;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Application.Common;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Search;

/// <summary>
/// <see cref="SearchKnowledgeQueryHandler"/> (<c>GET /v1/search</c>) için testler; dil modeli olmadan yalnızca arama yapılır.
/// Odak <c>mode</c> parametresidir: değerlendirme aracı aynı soruları <c>mode=lexical</c> ve <c>mode=hybrid</c> ile
/// çalıştırarak vektör aramasının katkısını ölçer ve arama hatalarını yanıt üretimi hatalarından ayırır.
/// </summary>
/// <remarks>
/// Gerçek <c>KnowledgeIndex</c>, sabit vektörler döndüren <see cref="FakeTextEmbedder"/> ile kurulur; ağa çıkılmaz.
/// </remarks>
public sealed class SearchKnowledgeQueryHandlerTests
{
    /// <summary>
    /// Hibrit modda çalışabilen küçük bir gerçek indeks kurar: tek chunk'ın kayıtlı vektörü ([0, 1]) fake embedder'ın
    /// varsayılan model adıyla işaretlenir ve sorgu vektörü de aynı boyutta ([0, 1]) üretilir.
    /// </summary>
    /// <remarks>
    /// <c>KnowledgeIndex</c> kayıtlı vektörleri yalnızca yapılandırılmış embedding modeli üretmişse ve boyutlar tutuyorsa
    /// kullanır; ikisinden biri uymasa indeks sessizce BM25'e düşer ve "hybrid" beklentisi sınanamazdı.
    /// </remarks>
    private static KnowledgeIndex HybridIndex()
    {
        var index = new KnowledgeIndex(new FakeTextEmbedder { QueryVector = _ => [0f, 1f] }, Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);
        var document = new KnowledgeDocument("para", "para", "Para İadesi", "1.0", new DateOnly(2025, 1, 1), DocumentStatus.Active, DocumentCategory.Policy, null, "hash");
        document.AddChunk("Para İadesi", "Ücret 5 iş günü içinde kartınıza aktarılır.").SetEmbedding([0f, 1f], FakeTextEmbedder.DefaultModel);
        index.Rebuild([document]);
        return index;
    }

    /// <summary>
    /// "para iadesi" sorgusunu (topK = 3) verilen <paramref name="mode"/> ile, her çağrıda yeniden kurulan hibrit indeks
    /// üzerinde çalıştırır.
    /// </summary>
    private static Task<ApiResult<SearchResultDto>> SearchAsync(string? mode)
    {
        var index = HybridIndex();
        var handler = new SearchKnowledgeQueryHandler(index, new KnowledgeBusinessRules(index), Options.Create(new RetrievalOptions()));
        return handler.Handle(new SearchKnowledgeQuery("para iadesi", 3, mode), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// <c>mode</c> parametresinin arama türünü seçtiğini doğrular: verilmezse ya da "hybrid" ise vektörler kullanılabildiği
    /// için hibrit arama yapılır; "lexical" (büyük/küçük harf duyarsız, "LEXICAL" da kabul edilir) sorgu vektörünü devre dışı
    /// bırakıp yalnızca BM25 kullanır.
    /// </summary>
    /// <remarks>
    /// Değerlendirmedeki "yalnızca BM25 ile 10/12, hibrit ile 12/12 arama isabeti" karşılaştırması bu parametreye dayanır;
    /// <c>mode=lexical</c> yok sayılsaydı iki ölçüm aynı şeyi ölçer ve karşılaştırma anlamsızlaşırdı.
    /// </remarks>
    [Theory]
    [InlineData(null, "hybrid")]
    [InlineData("hybrid", "hybrid")]
    [InlineData("lexical", "lexical")]
    [InlineData("LEXICAL", "lexical")]
    public async Task The_mode_parameter_chooses_between_bm25_only_and_hybrid_retrieval(string? mode, string expectedMode)
    {
        var result = await SearchAsync(mode);

        result.Data!.RetrievalMode.ShouldBe(expectedMode);
    }

    /// <summary>
    /// Tanınmayan bir modun ("semantic") 400 ve "mode yalnızca 'lexical' veya 'hybrid' olabilir." mesajıyla reddedildiğini
    /// doğrular. Sessizce varsayılana düşmek, istemcinin istediğinden farklı bir şeyi ölçmesine yol açardı; açık bir hata
    /// yazım yanlışını hemen gösterir.
    /// </summary>
    [Fact]
    public async Task An_unknown_mode_is_rejected()
    {
        var result = await SearchAsync("semantic");

        result.StatusCode.ShouldBe(400);
        result.Message.ShouldBe("mode yalnızca 'lexical' veya 'hybrid' olabilir.");
    }
}
