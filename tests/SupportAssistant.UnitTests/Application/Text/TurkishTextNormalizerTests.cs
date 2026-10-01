using Knowledge.Application.Text;
using Shouldly;

namespace SupportAssistant.UnitTests.Application.Text;

public sealed class TurkishTextNormalizerTests
{
    [Theory]
    [InlineData("İADE Süresi", "iade suresi")]
    [InlineData("IŞIK", "isik")]
    [InlineData("Garanti süresi kaç gün?", "garanti suresi kac gun")]
    [InlineData("Wi-Fi'ye 2.4 GHz", "wi fi ye 2 4 ghz")]
    [InlineData("  çok   boşluk\n\tvar  ", "cok bosluk var")]
    public void Normalize_lowercases_folds_turkish_letters_and_strips_punctuation(string input, string expected)
    {
        TurkishTextNormalizer.Normalize(input).ShouldBe(expected);
    }
}
