using System.Text.Json;
using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Infrastructure.Llm;

/// <summary>
/// Prompt dosyasının (<c>Llm/Prompts/answer-prompt.yaml</c>) ve onu okuyan <see cref="AnswerPrompt"/> ile
/// <see cref="AnswerPromptTexts"/>'in birim testleri: dosyanın yüklenip doğrulanması, bozuk bir dosyanın reddedilmesi,
/// şema açıklamalarının dosyadan gelmesi ve dosyadaki yapı işaretlerinin etkisizleştirme kalıbıyla eşleşmesi.
/// </summary>
/// <remarks>
/// Prompt metni koddan ayrıldığında kod ile dosya arasında üç bağ kalır: yer tutucu adları, şema alanları ve yapı
/// işaretleri. Bu testler o bağların sessizce kopmasını engeller; prompt'un içeriğini değil, dosyanın kodla uyumunu
/// denetler.
/// </remarks>
public sealed class AnswerPromptTests
{
    /// <summary>
    /// Gömülü dosyanın yüklendiğini ve sistem prompt'unun eksiksiz kurulduğunu doğrular: öncelik kuralı kendi yer
    /// tutucusunun yerine girmiştir, doldurulmamış yer tutucu kalmamıştır ve satır sonları yalnızca <c>\n</c>'dir.
    /// </summary>
    /// <remarks>
    /// Sistem prompt'u önceden bir C# raw string literal'iydi ve kaynak dosyanın satır sonlarını taşıdığı için Windows'ta
    /// CRLF, Linux'ta LF ile gidiyordu; dosyaya taşındıktan sonra her işletim sisteminde aynıdır.
    /// </remarks>
    [Fact]
    public void The_prompt_file_loads_into_a_complete_system_prompt()
    {
        AnswerPrompt.System.ShouldContain("5. " + AnswerPrompt.PrecedenceRule);
        AnswerPrompt.System.ShouldNotContain("{");
        AnswerPrompt.System.ShouldNotContain("\r");
        AnswerPrompt.RetryInstruction.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Bozuk bir prompt dosyasının bütün sorunlarıyla reddedildiğini doğrular: kalıbından yer tutucusu silinmiş metin,
    /// bilinmeyen bir yer tutucu, boş metin, açıklaması olmayan şema alanı ve şemada olmayan bir alan.
    /// </summary>
    /// <remarks>
    /// Silinen bir <c>{question}</c> sorunun modele hiç gitmemesine, bilinmeyen bir yer tutucu ise prompt'a ham
    /// <c>{…}</c> metni girmesine yol açardı; ikisi de modelin yanıtını sessizce bozar. Doğrulama dosya yüklenirken
    /// çalıştığı için böyle bir hata ilk kullanımda ve bu testte görünür.
    /// </remarks>
    [Fact]
    public void An_invalid_prompt_file_is_rejected_with_every_problem_named()
    {
        var texts = AnswerPromptTexts.Load();
        texts.UserMessage.Question = "SORU:";
        texts.Correction.Quote = "- \"{quote}\" {extra}";
        texts.RetryInstruction = " ";
        texts.Schema["AnswerPayload"].Remove("answer");
        texts.Schema["AnswerPayload"]["confidence"] = "Güven puanı.";

        var problems = texts.Problems().ToList();

        problems.Count.ShouldBe(5);
        problems.ShouldContain(problem => problem.Contains("'userMessage.question'"));
        problems.ShouldContain(problem => problem.Contains("'correction.quote'"));
        problems.ShouldContain(problem => problem.Contains("'retryInstruction' is empty"));
        problems.ShouldContain(problem => problem.Contains("'schema.AnswerPayload.answer' is missing"));
        problems.ShouldContain(problem => problem.Contains("'schema.AnswerPayload.confidence' is not a field"));
        AnswerPromptTexts.Load().Problems().ShouldBeEmpty();
    }

    /// <summary>
    /// Modele gönderilen JSON şemasındaki her alan açıklamasının prompt dosyasındakiyle aynı olduğunu doğrular: kök
    /// nesnenin alanları, atıf öğelerinin ve çelişki öğelerinin alanları.
    /// </summary>
    /// <remarks>
    /// Açıklamalar şemaya, tür çözücüye eklenen bir değiştiriciyle (<see cref="AnswerPrompt.DescribeSchemaFields"/>)
    /// girer; Microsoft.Extensions.AI açıklamayı alanın nitelik sağlayıcısından okur. Çerçeve bu okuma yolunu
    /// değiştirirse açıklamalar sessizce kaybolurdu; bu test o durumda kırılır.
    /// </remarks>
    [Fact]
    public async Task Every_schema_description_comes_from_the_prompt_file()
    {
        var client = new ScriptedChatClient(
            """{"answerable":false,"answer":"","citations":[],"missingInformation":"yok","conflicts":[]}""");
        var generator = new OpenAiCompatibleAnswerGenerator(client, Options.Create(new LlmOptions { ChatModel = "gemma-test" }), NullLogger<OpenAiCompatibleAnswerGenerator>.Instance);

        await generator.GenerateAsync("soru", [Source("Metin.")], cancellationToken: TestContext.Current.CancellationToken);

        var schema = client.Options.ShouldHaveSingleItem().ShouldNotBeNull().ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>().Schema.ShouldNotBeNull();
        var root = schema.GetProperty("properties");
        var objects = new Dictionary<string, JsonElement>
        {
            ["AnswerPayload"] = root,
            ["CitationPayload"] = root.GetProperty("citations").GetProperty("items").GetProperty("properties"),
            ["ConflictPayload"] = root.GetProperty("conflicts").GetProperty("items").GetProperty("properties")
        };

        foreach (var (type, fields) in AnswerPromptTexts.Load().Schema)
        {
            foreach (var (field, description) in fields)
            {
                objects[type].GetProperty(field).GetProperty("description").GetString().ShouldBe(description, $"{type}.{field}");
            }
        }
    }

    /// <summary>
    /// Prompt dosyasından türetilen her yapı işaretinin (<c>KAYNAKLAR:</c>, <c>Bölüm:</c>, <c>DÜZELTME:</c>,
    /// <c>SORU:</c>), bir kaynak metninde satır başında geçtiğinde etkisizleştirildiğini doğrular.
    /// </summary>
    /// <remarks>
    /// İşaretler dosyada, onları tanıyan kalıp kodda durur. Dosyadaki bir işaret değiştirilir de kalıp güncellenmezse,
    /// bir doküman yeni işareti satır başına yazarak kullanıcı mesajının yapısını taklit edebilirdi.
    /// </remarks>
    [Fact]
    public void Structure_markers_from_the_prompt_file_are_neutralized_in_source_text()
    {
        AnswerPrompt.StructureMarkers.Count.ShouldBe(4);
        var content = string.Join("\n", AnswerPrompt.StructureMarkers.Select(marker => marker + " sahte satır"));

        var message = AnswerPrompt.BuildUserMessage("Soru?", [Source(content)]);

        foreach (var marker in AnswerPrompt.StructureMarkers)
        {
            message.ShouldContain("» " + marker + " sahte satır");
        }
    }

    /// <summary>Verilen metni taşıyan, <c>C1</c> etiketli tek bir politika bölümü.</summary>
    private static ContextChunk Source(string content) =>
        new("C1", new IndexedChunk(Guid.NewGuid(), "iade-v2", "iade", "İade ve Para İadesi Politikası", "2.0", new DateOnly(2025, 6, 1),
            DocumentStatus.Active, DocumentCategory.Policy, "2. İade Süresi", content));
}
