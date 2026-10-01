using Knowledge.Application.Abstractions;
using Knowledge.Application.Options;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Infrastructure.Search;

public sealed class KnowledgeIndexTests
{
    private static KnowledgeIndex CreateIndex(ITextEmbedder embedder) =>
        new(embedder, Options.Create(new RetrievalOptions()), NullLogger<KnowledgeIndex>.Instance);

    private static KnowledgeDocument Document(string id, string title, params (string Path, string Content, float[]? Vector, string Model)[] sections)
    {
        var document = new KnowledgeDocument(id, id, title, "1.0", new DateOnly(2025, 1, 1), DocumentStatus.Active, DocumentCategory.Policy, null, $"hash-{id}");

        foreach (var section in sections)
        {
            var chunk = document.AddChunk(section.Path, section.Content);

            if (section.Vector is not null)
            {
                chunk.SetEmbedding(section.Vector, section.Model);
            }
        }

        return document;
    }

    private static readonly KnowledgeDocument ReturnPolicy = Document("iade", "İade Politikası",
        ("2. İade Süresi", "Ürünü teslim aldığınız tarihten itibaren 30 gün içinde iade edebilirsiniz.", null, ""),
        ("5. İade Kargo Ücreti", "İade kargosu ücretsizdir.", null, ""));

    private static readonly KnowledgeDocument Shipping = Document("kargo", "Kargo ve Teslimat",
        ("2. Kargo Ücreti", "750 TL ve üzeri siparişlerde kargo ücretsizdir.", null, ""));

    [Fact]
    public void Index_is_not_ready_until_it_is_built()
    {
        var index = CreateIndex(new FakeTextEmbedder(enabled: false));

        index.IsReady.ShouldBeFalse();

        index.Rebuild([ReturnPolicy, Shipping]);

        index.IsReady.ShouldBeTrue();
        index.Status.DocumentCount.ShouldBe(2);
        index.Status.ChunkCount.ShouldBe(3);
    }

    [Fact]
    public async Task Search_finds_the_section_for_a_question_typed_without_turkish_characters()
    {
        var index = CreateIndex(new FakeTextEmbedder(enabled: false));
        index.Rebuild([ReturnPolicy, Shipping]);

        var result = index.Search(await index.PrepareAsync("iade suresi kac gun", TestContext.Current.CancellationToken), topK: 3);

        result.Mode.ShouldBe(RetrievalMode.Lexical);
        result.Hits[0].Chunk.SectionPath.ShouldBe("2. İade Süresi");
        result.MaxLexicalCoverage.ShouldBe(1.0, 1e-9);
    }

    [Fact]
    public async Task Hybrid_search_also_returns_semantic_matches_without_shared_words()
    {
        var embedder = new FakeTextEmbedder { QueryVector = _ => [0f, 1f] };
        var index = CreateIndex(embedder);
        index.Rebuild(
        [
            Document("para", "Para İadesi", ("Para İadesi", "Ücret 5 iş günü içinde kartınıza aktarılır.", [0f, 1f], FakeTextEmbedder.DefaultModel)),
            Document("kargo", "Kargo", ("Kargo Ücreti", "750 TL üzeri siparişlerde kargo ücretsizdir.", [1f, 0f], FakeTextEmbedder.DefaultModel))
        ]);

        var result = index.Search(await index.PrepareAsync("paramı geri alırım", TestContext.Current.CancellationToken), topK: 2);

        result.Mode.ShouldBe(RetrievalMode.Hybrid);
        result.Hits[0].Chunk.DocumentId.ShouldBe("para");
        result.Hits[0].DenseScore.ShouldNotBeNull();
        result.Hits[0].DenseScore!.Value.ShouldBe(1.0, 1e-6);
        result.MaxDenseScore.ShouldBe(1.0, 1e-6);
    }

    [Fact]
    public async Task Search_stays_lexical_when_stored_embeddings_come_from_another_model()
    {
        var index = CreateIndex(new FakeTextEmbedder(modelName: "new-model"));
        index.Rebuild([Document("iade", "İade", ("İade Süresi", "30 gün içinde iade edebilirsiniz.", [1f, 0f], "old-model"))]);

        var result = index.Search(await index.PrepareAsync("iade süresi", TestContext.Current.CancellationToken), topK: 3);

        result.Mode.ShouldBe(RetrievalMode.Lexical);
        index.Status.Mode.ShouldBe(RetrievalMode.Lexical);
        result.Hits.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Search_falls_back_to_lexical_when_the_query_cannot_be_embedded()
    {
        var embedder = new FakeTextEmbedder();
        var index = CreateIndex(embedder);
        index.Rebuild([Document("iade", "İade", ("İade Süresi", "30 gün içinde iade edebilirsiniz.", [1f, 0f], FakeTextEmbedder.DefaultModel))]);
        embedder.Failure = new HttpRequestException("embedding server is down");

        var result = index.Search(await index.PrepareAsync("iade süresi", TestContext.Current.CancellationToken), topK: 3);

        result.Mode.ShouldBe(RetrievalMode.Lexical);
        result.Hits.ShouldHaveSingleItem().Chunk.SectionPath.ShouldBe("İade Süresi");
    }

    [Fact]
    public async Task Search_only_returns_chunks_accepted_by_the_filter()
    {
        var index = CreateIndex(new FakeTextEmbedder(enabled: false));
        index.Rebuild([ReturnPolicy, Shipping]);

        var result = index.Search(
            await index.PrepareAsync("ücretsiz kargo", TestContext.Current.CancellationToken),
            topK: 5,
            chunk => chunk.DocumentId == "kargo");

        result.Hits.ShouldNotBeEmpty();
        result.Hits.ShouldAllBe(hit => hit.Chunk.DocumentId == "kargo");
    }
}
