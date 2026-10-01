using Knowledge.Infrastructure.Search;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Search;

public sealed class SearchTokenizerTests
{
    [Fact]
    public void Tokenize_drops_stopwords_and_truncates_words_to_five_character_stems()
    {
        SearchTokenizer.Tokenize("Ürünü kaç gün içinde iade edebilirim?")
            .ShouldBe(["urunu", "gun", "iade", "edebi"]);
    }

    [Theory]
    [InlineData("iade suresi kac gun")]
    [InlineData("İade süresi kaç gün?")]
    public void Tokenize_produces_the_same_terms_with_or_without_turkish_characters(string question)
    {
        SearchTokenizer.Tokenize(question).ShouldBe(["iade", "sures", "gun"]);
    }

    [Fact]
    public void Tokenize_keeps_numbers_and_drops_single_letters()
    {
        SearchTokenizer.Tokenize("E-posta 2.4 GHz ve 750 TL")
            .ShouldBe(["posta", "2", "4", "ghz", "750", "tl"]);
    }
}
