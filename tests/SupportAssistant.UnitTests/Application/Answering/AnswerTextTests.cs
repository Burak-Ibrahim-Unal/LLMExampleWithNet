using Knowledge.Application.Answering;
using Shouldly;

namespace SupportAssistant.UnitTests.Application.Answering;

public sealed class AnswerTextTests
{
    [Theory]
    [InlineData("Ürünü 30 gün içinde iade edebilirsiniz [C1].", "Ürünü 30 gün içinde iade edebilirsiniz.")]
    [InlineData("30 gün içinde iade edebilirsiniz [C1, \"Ürünlerinizi 30 gün içinde iade edebilirsiniz.\"].", "30 gün içinde iade edebilirsiniz.")]
    [InlineData("Kargo ücretsizdir (C2) ve 750 TL üzeri siparişlerde geçerlidir.", "Kargo ücretsizdir ve 750 TL üzeri siparişlerde geçerlidir.")]
    [InlineData("İade kargosu ücretsizdir [C1][C3].", "İade kargosu ücretsizdir.")]
    [InlineData("  Kaynak işareti yok.  ", "Kaynak işareti yok.")]
    public void Clean_removes_source_markers_the_model_left_in_the_answer(string answer, string expected)
    {
        AnswerText.Clean(answer).ShouldBe(expected);
    }

    [Theory]
    [InlineData("30 gün içinde iade edebilirsiniz. \"Müşteriler 30 gün içinde iade talebinde bulunabilir.\"", "30 gün içinde iade edebilirsiniz.")]
    [InlineData("30 gün içinde iade edebilirsiniz. “Müşteriler 30 gün içinde iade talebinde bulunabilir.”", "30 gün içinde iade edebilirsiniz.")]
    [InlineData("Politikaya göre \"Müşteriler 30 gün içinde iade talebinde bulunabilir.\" Bu süre teslimattan başlar.", "Politikaya göre Bu süre teslimattan başlar.")]
    public void Clean_removes_quoted_copies_of_cited_text_from_the_answer(string answer, string expected)
    {
        AnswerText.Clean(answer, ["Müşteriler 30 gün içinde iade talebinde bulunabilir."]).ShouldBe(expected);
    }
}
