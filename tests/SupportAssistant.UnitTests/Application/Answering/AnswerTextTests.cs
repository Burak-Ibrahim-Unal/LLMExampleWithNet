using Knowledge.Application.Answering;
using Shouldly;

namespace SupportAssistant.UnitTests.Application.Answering;

/// <summary>
/// <see cref="AnswerText"/> temizleme kuralları için birim testleri. Sistem prompt'u yanıt metnine kaynak işareti koymayı
/// yasaklasa da küçük modeller zaman zaman "[C1]", "(C2)" gibi işaretleri ya da alıntıladıkları metnin tırnak içindeki
/// kopyasını yanıta bırakır. Kaynaklar yanıtta ayrı bir alanda (<c>sources</c>) taşındığı için müşteriye giden metnin bu
/// kalıntılardan arındırılması gerekir.
/// </summary>
/// <remarks>
/// Saf (yan etkisiz) bir fonksiyon test edildiğinden fake ya da altyapı gerekmez; girdi ve beklenen çıktı
/// <c>[InlineData]</c> satırlarında birebir okunur.
/// </remarks>
public sealed class AnswerTextTests
{
    /// <summary>
    /// Modelin yanıt metninde bıraktığı kaynak işaretlerinin silindiğini doğrular: cümle sonundaki <c>[C1]</c>, içine alıntı
    /// gömülmüş <c>[C1, "…"]</c> biçimi, cümle ortasındaki <c>(C2)</c> ve bitişik <c>[C1][C3]</c>. İşaretten önceki boşluk
    /// da silindiği için noktalama yerinde kalır ve çift boşluk oluşmaz; işaret içermeyen metne yalnızca kırpma (trim)
    /// uygulanır.
    /// </summary>
    /// <remarks>
    /// Bu test kırılırsa müşteri "…iade edebilirsiniz [C1]." gibi iç etiketlerle dolu, okunması zor bir metin görür. Son
    /// satır ise temizliğin işaret içermeyen bir metni bozmadığını güvenceye alır.
    /// </remarks>
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

    /// <summary>
    /// Atıf yapılan kaynak metnin tırnak içindeki kopyasının yanıttan çıkarıldığını doğrular: düz çift tırnak, modellerin
    /// sık ürettiği tipografik (kıvrık) tırnak “ ” ve cümlenin ortasına yerleştirilmiş alıntı. Ortadan çıkarılan alıntının
    /// bıraktığı çift boşluk teke indirilir.
    /// </summary>
    /// <remarks>
    /// Alıntı zaten <c>sources</c> alanında gösterildiği için yanıtta tekrarlanması metni uzatır ve doğal olmayan bir dil
    /// yaratır. Üçüncü satırdaki "Politikaya göre Bu süre…" çıktısı bilinçli olarak sabitlenmiştir: <c>Clean</c> yalnızca
    /// siler, dilbilgisini yeniden yazmaz. Üretim kodu 20 karakterden kısa alıntıları ("30 gün" gibi) normal cümlenin
    /// parçası saydığı için hiç silmez; buradaki alıntı bu sınırın üzerindedir.
    /// </remarks>
    [Theory]
    [InlineData("30 gün içinde iade edebilirsiniz. \"Müşteriler 30 gün içinde iade talebinde bulunabilir.\"", "30 gün içinde iade edebilirsiniz.")]
    [InlineData("30 gün içinde iade edebilirsiniz. “Müşteriler 30 gün içinde iade talebinde bulunabilir.”", "30 gün içinde iade edebilirsiniz.")]
    [InlineData("Politikaya göre \"Müşteriler 30 gün içinde iade talebinde bulunabilir.\" Bu süre teslimattan başlar.", "Politikaya göre Bu süre teslimattan başlar.")]
    public void Clean_removes_quoted_copies_of_cited_text_from_the_answer(string answer, string expected)
    {
        AnswerText.Clean(answer, ["Müşteriler 30 gün içinde iade talebinde bulunabilir."]).ShouldBe(expected);
    }
}
