namespace SupportAssistant.Eval;

/// <summary>
/// Değerlendirme aracının komut satırı seçenekleri. Değişmez bir record olarak tutulur; ayrıştırma her parametrede
/// <c>with</c> ifadesiyle yeni bir kopya üretir.
/// </summary>
/// <param name="BaseUrl">Değerlendirilecek, çalışan API'nin taban adresi (<c>--base-url</c>); varsayılanı API'nin yerel geliştirme profilindeki adrestir.</param>
/// <param name="QuestionsPath">Soru dosyası (<c>--questions</c>); varsayılan <c>eval/questions.json</c>.</param>
/// <param name="OutputPath">Rapor klasörü (<c>--output</c>); varsayılan <c>eval/results</c>.</param>
/// <param name="Label">
/// İsteğe bağlı koşu adı (<c>--label</c>, ör. "thinking-on"); verilirse sonuçlar çıktı klasörünün bu adlı alt klasörüne
/// yazılır, böylece farklı yapılandırmalarla yapılan koşular varsayılan raporun üzerine yazmaz.
/// </param>
public sealed record EvalOptions(string BaseUrl, string QuestionsPath, string OutputPath, string? Label)
{
    /// <summary>Hatalı ya da eksik argümanda hata mesajının sonuna eklenen kullanım metni.</summary>
    public const string Usage =
        "Kullanım: dotnet run --project tools/SupportAssistant.Eval -- [--base-url http://localhost:5031] " +
        "[--questions eval/questions.json] [--output eval/results] [--label ad]";

    /// <summary>
    /// <c>--ad değer</c> çiftlerini ayrıştırır. Verilmeyen her seçenek varsayılanında kalır; bu yüzden araç argümansız bir
    /// <c>dotnet run</c> ile çalışır. Değeri eksik ya da bilinmeyen bir parametrede kullanım metnini içeren
    /// bir <see cref="ArgumentException"/> fırlatılır; <c>Program.cs</c> bunu yakalayıp 1 koduyla çıkar.
    /// </summary>
    public static EvalOptions Parse(string[] args)
    {
        var options = new EvalOptions("http://localhost:5031", Path.Combine("eval", "questions.json"), Path.Combine("eval", "results"), null);

        for (var i = 0; i < args.Length; i++)
        {
            // Her parametre bir değer alır; değer, adın hemen ardındaki argümandır.
            var value = i + 1 < args.Length ? args[i + 1] : throw new ArgumentException($"'{args[i]}' için değer eksik. {Usage}");

            options = args[i] switch
            {
                "--base-url" => options with { BaseUrl = value },
                "--questions" => options with { QuestionsPath = value },
                "--output" => options with { OutputPath = value },
                "--label" => options with { Label = value },
                _ => throw new ArgumentException($"Bilinmeyen parametre: {args[i]}. {Usage}")
            };

            // Değer tüketildi; döngünün bir sonraki adımı yeni bir parametre adından başlar.
            i++;
        }

        return options;
    }

    /// <summary>
    /// Göreli yolları, çalışma klasörünün kendisinden başlayarak "eval" klasörünü içeren ilk üst klasöre göre çözer;
    /// mutlak yollar olduğu gibi döner.
    /// </summary>
    /// <remarks>
    /// Böylece araç depo kökünden (<c>dotnet run --project tools/SupportAssistant.Eval</c>), kendi proje klasöründen ya da
    /// başka bir alt klasörden çalıştırıldığında aynı <c>eval/</c> klasörünü bulur ve raporu her seferinde aynı yere yazar.
    /// Hiçbir üst klasörde "eval" yoksa yol çalışma klasörüne göre çözülür.
    /// </remarks>
    public static string ResolveFromRepository(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "eval")))
            {
                return Path.Combine(directory.FullName, path);
            }
        }

        return Path.GetFullPath(path);
    }
}
