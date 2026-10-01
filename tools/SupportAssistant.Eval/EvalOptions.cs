namespace SupportAssistant.Eval;

/// <param name="Label">Optional run name (e.g. "thinking-on"); results then go to a subfolder of the output folder.</param>
public sealed record EvalOptions(string BaseUrl, string QuestionsPath, string OutputPath, string? Label)
{
    public const string Usage =
        "Kullanım: dotnet run --project tools/SupportAssistant.Eval -- [--base-url http://localhost:5031] " +
        "[--questions eval/questions.json] [--output eval/results] [--label ad]";

    public static EvalOptions Parse(string[] args)
    {
        var options = new EvalOptions("http://localhost:5031", Path.Combine("eval", "questions.json"), Path.Combine("eval", "results"), null);

        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : throw new ArgumentException($"'{args[i]}' için değer eksik. {Usage}");

            options = args[i] switch
            {
                "--base-url" => options with { BaseUrl = value },
                "--questions" => options with { QuestionsPath = value },
                "--output" => options with { OutputPath = value },
                "--label" => options with { Label = value },
                _ => throw new ArgumentException($"Bilinmeyen parametre: {args[i]}. {Usage}")
            };

            i++;
        }

        return options;
    }

    /// <summary>Relative paths are resolved from the first ancestor of the working directory that contains the "eval" folder.</summary>
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
