using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.Commands.AskQuestion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Answering;

/// <summary>
/// <see cref="AskQuestionCommandHandler"/>'ın atıf denetimi (Kapı 3) ve model çağrı bütçesi testleri: yalnızca
/// alıntısı atıf yapılan bölümde doğrulanan atıflar yanıtın dayanağı olur, doğrulanamayan alıntılar bir düzeltme turuna
/// gider ve soru başına en fazla iki gerçek model isteği yapılır. Kurulum <see cref="AskQuestionHandlerTestBase"/>'tedir.
/// </summary>
public sealed class AskQuestionCitationTests : AskQuestionHandlerTestBase
{
    /// <summary>
    /// Modelin <c>answerable=true</c> deyip yalnızca kendisine verilmemiş bir kaynağa ("C9") atıf yaptığı durumu (Kapı 3)
    /// sınar: kabul edilebilir atıf olmadığı için model bir kez düzeltme talimatıyla yeniden çağrılır (doğrulanacak alıntı
    /// olmadığından geri bildirimdeki alıntı listesi boştur); ikinci yanıt da aynıysa yanıt <c>NoValidCitations</c>
    /// gerekçesiyle reddedilir ve hiçbir kaynak gösterilmez.
    /// </summary>
    /// <remarks>
    /// Model, verilen bağlamın dışına atıf yaparak kaynaksız bir iddiayı kaynaklıymış gibi sunamamalıdır. Bu test kırılırsa
    /// uydurma etiketlerle "desteklenen" yanıtlar müşteriye kaynaklı bir yanıt olarak ulaşabilir ya da düzeltme turu
    /// sınırsız tekrarlanabilir.
    /// </remarks>
    [Fact]
    public async Task An_answer_citing_no_provided_source_is_refused()
    {
        _generator.Respond = (_, _) => FakeAnswerGenerator.Answer("30 gün.", new GeneratedCitation("C9", "30 gün"));

        var result = await AskAsync("İade süresi kaç gün?");

        _generator.Calls.ShouldBe(2);
        _generator.Feedbacks[1].ShouldNotBeNull().UnverifiedQuotes.ShouldBeEmpty();
        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.NoValidCitations);
        result.Data.Sources.ShouldBeEmpty();
    }

    /// <summary>
    /// İlk yanıtın tek atfı doğru bölüme işaret eder ama alıntısı kaynakta yoktur ("İade süresi 900 gündür."). Model bir
    /// kez daha çağrılır; ikinci çağrının geri bildirimi doğrulanamayan alıntıyı aynen içerir. İkinci yanıt kaynağı birebir
    /// alıntıladığı için kabul edilir. Tanılama iki model çağrısını ve iki çağrının toplam token sayısını gösterir.
    /// </summary>
    /// <remarks>
    /// Doğrulanmış alıntı yanıtın tek dayanağıdır: kaynakta olmayan bir alıntıyla desteklenen bir yanıt, kaynaklı bir yanıt
    /// gibi gösterilemez. Ama yalnızca alıntıyı yanlış kopyalayan bir modeli hemen reddetmek doğru yanıtları da kaybettirir;
    /// tek düzeltme turu bu ikisini dengeler. Bu test kırılırsa ya uydurma alıntılar yanıta dönüşür ya da düzeltilebilir
    /// yanıtlar gereksiz yere reddedilir.
    /// </remarks>
    [Fact]
    public async Task An_answer_without_a_verifiable_quote_gets_one_correction_round()
    {
        _generator.Respond = (question, context) => _generator.Calls == 1
            ? FakeAnswerGenerator.Answer("İade süresi 900 gündür.", Cite(context, "iade-v2", "2. İade Süresi", "İade süresi 900 gündür."))
            : FakeAnswerGenerator.QuoteFirstSource(question, context);

        var result = await AskAsync("İade süresi kaç gün?");

        _generator.Calls.ShouldBe(2);
        _generator.Feedbacks[0].ShouldBeNull();
        _generator.Feedbacks[1].ShouldNotBeNull().UnverifiedQuotes.ShouldBe(["İade süresi 900 gündür."]);
        result.Data!.Answerable.ShouldBeTrue();
        result.Data.Sources.ShouldNotBeEmpty();
        result.Data.Sources.ShouldAllBe(source => source.QuoteVerified);
        result.Data.Diagnostics.ModelCalls.ShouldBe(2);
        result.Data.Diagnostics.InputTokens.ShouldBe(20);
        result.Data.Diagnostics.OutputTokens.ShouldBe(10);
    }

    /// <summary>
    /// Model düzeltme turunda da doğrulanabilir bir alıntı vermezse (uydurma alıntı ya da boş alıntı) yanıtın
    /// <c>NoValidCitations</c> gerekçesiyle reddedildiğini ve modelin en fazla iki kez çağrıldığını doğrular.
    /// </summary>
    /// <remarks>
    /// Arkadaş incelemesinin kabul ölçütü: kaynak "30 gün" derken "900 gün" ya da boş bir alıntı başarılı bir yanıtı tek
    /// başına destekleyemez. Boş alıntı da doğrulanmış sayılmaz; aksi hâlde bir etiket yazmak yanıtı kaynaklı göstermeye
    /// yeterdi.
    /// </remarks>
    [Theory]
    [InlineData("İade süresi 900 gündür.")]
    [InlineData("")]
    public async Task An_answer_whose_quotes_stay_unverifiable_is_refused(string quote)
    {
        _generator.Respond = (_, context) => FakeAnswerGenerator.Answer("İade süresi 900 gündür.", Cite(context, "iade-v2", "2. İade Süresi", quote));

        var result = await AskAsync("İade süresi kaç gün?");

        _generator.Calls.ShouldBe(2);
        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.NoValidCitations);
        result.Data.Sources.ShouldBeEmpty();
    }

    /// <summary>
    /// Bir yanıtın atıflarından biri doğrulanır, diğeri doğrulanamazsa ("İade kargosu 50 TL'dir." kaynakta yok) yanıtın
    /// kabul edildiğini, ancak kaynak listesinde yalnızca doğrulanmış atfın göründüğünü doğrular. Doğrulanmış bir dayanak
    /// olduğu için düzeltme turuna girilmez (tek model çağrısı).
    /// </summary>
    /// <remarks>
    /// Yanıtın kaynakları okuyucunun güvenebileceği kanıtlardır; doğrulanamayan bir alıntıyı kaynak diye göstermek "her
    /// yanıt kullandığı bölümü gösterir" taahhüdünü yanıltıcı kılardı. Düzeltme turu ise yalnızca hiç dayanak kalmadığında
    /// harcanır; çoğu yanıtın gecikmesi değişmez.
    /// </remarks>
    [Fact]
    public async Task Only_citations_with_verified_quotes_are_listed_as_sources()
    {
        _generator.Respond = (_, context) => FakeAnswerGenerator.Answer(
            "İade süresi 30 gündür.",
            Cite(context, "iade-v2", "2. İade Süresi", "30 gün içinde iade edebilirsiniz"),
            Cite(context, "iade-v2", ReturnShippingSection, "İade kargosu 50 TL'dir."));

        var result = await AskAsync("İade süresi kaç gün?");

        _generator.Calls.ShouldBe(1);
        var source = result.Data!.Sources.ShouldHaveSingleItem();
        source.Section.ShouldBe("2. İade Süresi");
        source.QuoteVerified.ShouldBeTrue();
    }

    /// <summary>
    /// Modelin yanıt metni yalnızca bir kaynak işaretinden (<c>[C1]</c>) oluştuğunda temizlikten sonra metin boş kalır; bu
    /// durumda yanıt olarak doğrulanmış atfın alıntı metninin kullanıldığını doğrular. Yanıtta ikinci, doğrulanamayan bir
    /// alıntı ("Ürünü 900 gün içinde iade edebilirsiniz.") da vardır; bu alıntı yanıt metnine girmez ve yanıt yalnızca
    /// "Ürünü teslim aldıktan sonra 30 gün içinde iade edebilirsiniz." olur.
    /// </summary>
    /// <remarks>
    /// Bu geri dönüş (fallback) olmasaydı, kaynağı doğru bulup metne yalnızca işaret yazan bir model yanıtı ya boş metinle
    /// döner ya da gereksiz yere reddedilirdi. Yedek yalnızca doğrulanmış alıntıları kullanır; aksi hâlde kaynakta geçmeyen
    /// bir metin doğrudan müşteriye giden yanıt olurdu.
    /// </remarks>
    [Fact]
    public async Task When_cleaning_leaves_no_answer_text_the_verified_quote_is_used()
    {
        _generator.Respond = (_, context) => FakeAnswerGenerator.Answer(
            "[C1]",
            new GeneratedCitation(context[0].Label, context[0].Chunk.Content),
            new GeneratedCitation(context[0].Label, "Ürünü 900 gün içinde iade edebilirsiniz."));

        var result = await AskAsync("İade süresi kaç gün?");

        result.Data!.Answerable.ShouldBeTrue();
        result.Data.Answer.ShouldBe("Ürünü teslim aldıktan sonra 30 gün içinde iade edebilirsiniz.");
    }

    /// <summary>
    /// Üreticinin kendi şema düzeltmesiyle yaptığı çağrıların da soru başına model çağrısı bütçesinden düştüğünü doğrular:
    /// üretici ilk yanıt için iki çağrı harcadıysa (<c>Attempts = 2</c>) bütçe biter; alıntı doğrulanamasa bile düzeltme
    /// turu yapılmaz, yanıt <c>NoValidCitations</c> ile reddedilir ve tanılama iki gerçek çağrıyı gösterir.
    /// </summary>
    /// <remarks>
    /// Arkadaş incelemesinin ikinci geçişte bulduğu açık: handler'ın düzeltme turu ile üreticinin şema yeniden denemesi
    /// birleşince sunucuya 4 istek gidebiliyor, tanılama ise 2 diyordu. Bütçe artık gerçek çağrıları sayar; "en fazla iki
    /// çağrı" sözü hem gecikme hem maliyet için doğrudur.
    /// </remarks>
    [Fact]
    public async Task The_model_call_budget_includes_the_generator_retries()
    {
        _generator.Respond = (_, context) =>
            FakeAnswerGenerator.Answer("İade süresi 900 gündür.", Cite(context, "iade-v2", "2. İade Süresi", "İade süresi 900 gündür.")) with { Attempts = 2 };

        var result = await AskAsync("İade süresi kaç gün?");

        _generator.Calls.ShouldBe(1);
        result.Data!.RefusalReason.ShouldBe(RefusalReasons.NoValidCitations);
        result.Data.Diagnostics.ModelCalls.ShouldBe(2);
    }

    /// <summary>
    /// Arkadaş incelemesinin örneğini gerçek üretici ve gerçek handler'la birlikte yeniden oynatır: model önce <c>{}</c>,
    /// sonra uydurma alıntılı geçerli bir JSON, sonra yine <c>{}</c> ve en son doğru bir yanıt verecek şekilde
    /// programlanmıştır. Bütçe ortak olduğu için sunucuya yalnızca iki istek gider: <c>{}</c> üreticinin şema düzeltmesini,
    /// uydurma alıntı ise kalan bütçeyi tüketir; yanıt <c>NoValidCitations</c> ile reddedilir ve tanılama iki çağrı
    /// gösterir.
    /// </summary>
    /// <remarks>
    /// Eski hâlde aynı senaryo dört sohbet isteğiyle sonuçlanıyor ve tanılama iki gösteriyordu. Bu test, sınırın sahte
    /// üretici varsayımlarıyla değil gerçek bileşenlerin birleşimiyle de tuttuğunu kanıtlar.
    /// </remarks>
    [Fact]
    public async Task The_real_generator_and_handler_together_make_at_most_two_chat_requests()
    {
        const string invented = """{"answerable":true,"answer":"İade süresi 900 gündür.","citations":[{"chunkId":"C1","quote":"İade süresi 900 gündür."}],"missingInformation":"","conflicts":[]}""";
        const string correct = """{"answerable":true,"answer":"30 gün içinde iade edebilirsiniz.","citations":[{"chunkId":"C1","quote":"30 gün içinde iade edebilirsiniz"}],"missingInformation":"","conflicts":[]}""";
        var client = new ScriptedChatClient("{}", invented, "{}", correct);
        var generator = new Knowledge.Infrastructure.Llm.OpenAiCompatibleAnswerGenerator(
            client,
            Options.Create(new Knowledge.Infrastructure.Llm.LlmOptions { ChatModel = "gemma-test" }),
            NullLogger<Knowledge.Infrastructure.Llm.OpenAiCompatibleAnswerGenerator>.Instance);

        var result = await AskAsync("İade süresi kaç gün?", generator: generator);

        client.Requests.Count.ShouldBe(2);
        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.NoValidCitations);
        result.Data.Diagnostics.ModelCalls.ShouldBe(2);
        result.Data.Diagnostics.InputTokens.ShouldBe(200);
    }

    /// <summary>
    /// Düzeltme turunda üreticiye yalnızca kalan bütçenin verildiğini doğrular: ilk çağrıya 2, düzeltme turuna 1 deneme
    /// hakkı geçilir; düzeltilmiş yanıt kabul edilir ve tanılama toplam iki çağrı gösterir.
    /// </summary>
    /// <remarks>
    /// Düzeltme turu bütçenin tamamıyla çağrılsaydı üretici o turda da kendi şema denemesini yapabilir ve toplam çağrı
    /// sayısı üçe çıkabilirdi.
    /// </remarks>
    [Fact]
    public async Task The_correction_round_gets_only_the_remaining_budget()
    {
        _generator.Respond = (question, context) => _generator.Calls == 1
            ? FakeAnswerGenerator.Answer("İade süresi 900 gündür.", Cite(context, "iade-v2", "2. İade Süresi", "İade süresi 900 gündür."))
            : FakeAnswerGenerator.QuoteFirstSource(question, context);

        var result = await AskAsync("İade süresi kaç gün?");

        _generator.MaxAttempts.ShouldBe([2, 1]);
        result.Data!.Answerable.ShouldBeTrue();
        result.Data.Diagnostics.ModelCalls.ShouldBe(2);
    }
}
