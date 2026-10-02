using Knowledge.Application.Abstractions;
using Knowledge.Application.Exceptions;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace SupportAssistant.UnitTests.Infrastructure.Llm;

/// <summary>
/// <see cref="OpenAiCompatibleAnswerGenerator"/> birim testleri. Gerçek llama.cpp sunucusu yerine hazır yanıtları sırayla
/// döndüren ve gelen istekleri kaydeden sahte bir <c>IChatClient</c> kullanılır. Böylece prompt içeriği, örnekleme
/// ayarlarının iletilmesi, yapılandırılmış (JSON) yanıtın eşlenmesi, tek seferlik yeniden deneme ve hata sınıflandırması
/// (503 / 502) ağ ve model olmadan, tekrarlanabilir biçimde doğrulanır. MEAI'nin yapılandırılmış çıktı katmanı
/// (<c>GetResponseAsync&lt;T&gt;</c>) gerçek hâliyle çalışır; yalnızca model uç noktası sahtedir.
/// </summary>
public sealed class OpenAiCompatibleAnswerGeneratorTests
{
    /// <summary>
    /// Model endpoint'i sistemin dış sınırıdır: bu sahte istemci hazır yanıtları sırayla tekrar oynatır ve istekleri
    /// kaydeder. n'inci istek n'inci yanıtı alır, yanıtlar tükenirse sonuncusu tekrarlanır. Gerçek llama.cpp sunucusu
    /// gibi yanıtta model kimliği olarak bir dosya yolu ve token kullanım bilgisi döndürür.
    /// </summary>
    private sealed class ScriptedChatClient(params string[] replies) : IChatClient
    {
        /// <summary>
        /// Her çağrıda gönderilen mesaj listesi (sistem + kullanıcı; yeniden denemede ek olarak önceki yanıt ve düzeltici
        /// talimat). Listenin uzunluğu modele kaç kez gidildiğini gösterir.
        /// </summary>
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        /// <summary>Her çağrıda iletilen <c>ChatOptions</c>; örnekleme ayarlarının isteğe ulaştığını doğrulamak için tutulur.</summary>
        public List<ChatOptions?> Options { get; } = [];

        /// <summary>
        /// Ayarlanırsa her çağrı bu istisnayla başarısız olur; bağlantı reddi gibi taşıma katmanı hatalarını taklit eder.
        /// Bu durumda istek kaydedilmez.
        /// </summary>
        public Exception? Failure { get; set; }

        /// <summary>
        /// <c>Failure</c> ayarlıysa onunla başarısız olan bir görev döndürür; değilse isteği ve seçenekleri kaydedip
        /// sıradaki hazır yanıtı asistan mesajı olarak verir.
        /// </summary>
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                return Task.FromException<ChatResponse>(Failure);
            }

            Requests.Add(messages.ToList());
            Options.Add(options);
            var reply = replies[Math.Min(Requests.Count - 1, replies.Length - 1)];

            // llama.cpp, model kimliği olarak model dosyasının yolunu döndürür.
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply))
            {
                ModelId = "/home/someone/models/gemma.gguf",
                Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 20 }
            });
        }

        /// <summary>
        /// Üretici akışlı (streaming) çağrı kullanmaz; böyle bir çağrıya geçilirse test görünür biçimde başarısız olsun
        /// diye <c>NotSupportedException</c> fırlatır.
        /// </summary>
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        /// <summary>Ek servis sunmaz; yalnızca <c>IChatClient</c> sözleşmesi gereği vardır.</summary>
        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        /// <summary>Serbest bırakılacak kaynak yoktur; arayüz sözleşmesi gereği boştur.</summary>
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Şemaya uyan, yanıtlanabilir ve <c>C1</c>'e atıf yapan geçerli bir model yanıtı (JSON); alıntı C1 içeriğinde
    /// birebir geçer.
    /// </summary>
    private const string ValidReply =
        """{"answerable":true,"answer":"30 gün içinde iade edebilirsiniz.","citations":[{"chunkId":"C1","quote":"30 gün içinde iade edebilir"}],"missingInformation":"","conflicts":[]}""";

    /// <summary>
    /// Modele verilen tek bağlam parçası: <c>C1</c> etiketli, iade politikasının 2.0 sürümündeki "2. İade Süresi" bölümü.
    /// Prompt testi etiket, başlık, sürüm, tarih, tür ve bölüm bilgisinin bu kayıttan prompt'a taşındığını kontrol eder.
    /// </summary>
    private static readonly IReadOnlyList<ContextChunk> Context =
    [
        new("C1", new IndexedChunk(Guid.NewGuid(), "iade-v2", "iade", "İade ve Para İadesi Politikası", "2.0", new DateOnly(2025, 6, 1),
            DocumentStatus.Active, DocumentCategory.Policy, "2. İade Süresi", "Müşteriler ürünü 30 gün içinde iade edebilir."))
    ];

    /// <summary>
    /// Test edilen üreticiyi verilen sahte istemciyle kurar. Seçenek verilmezse yalnızca <c>ChatModel</c> = "gemma-test"
    /// ayarlanır; bu ad, raporlanan model adının sunucudan değil yapılandırmadan geldiğini kanıtlamak için kullanılır.
    /// </summary>
    private static OpenAiCompatibleAnswerGenerator Create(IChatClient client, LlmOptions? options = null) =>
        new(client, Options.Create(options ?? new LlmOptions { ChatModel = "gemma-test" }), NullLogger<OpenAiCompatibleAnswerGenerator>.Instance);

    /// <summary>
    /// Modele tek istek gittiğini, ilk mesajın sistem prompt'u olduğunu ve kullanıcı mesajının her kaynağı etiketi
    /// (<c>[C1]</c>), doküman başlığı, sürümü, yürürlük tarihi, türü, bölüm yolu ve içeriğiyle birlikte listelediğini;
    /// soruyu da içerdiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Model kaynaklara etiketle atıf yapar ve atıf doğrulayıcı (Kapı 3) yalnızca verilen C1..Cn etiketlerini kabul eder;
    /// etiket prompt'ta yoksa geçerli atıf üretilemez. Sürüm, tarih ve tür bilgisi sistem prompt'undaki kaynak önceliği
    /// kuralının (politika/prosedür &gt; kılavuz &gt; SSS, eşitlikte yeni tarih) uygulanabilmesi için gereklidir; bölüm
    /// yolu ise yanıtın hangi bölüme dayandığını gösterir.
    /// </remarks>
    [Fact]
    public async Task The_prompt_lists_each_source_with_its_label_document_version_and_section()
    {
        var client = new ScriptedChatClient(ValidReply);

        await Create(client).GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken);

        var messages = client.Requests.ShouldHaveSingleItem();
        messages[0].Role.ShouldBe(ChatRole.System);
        var prompt = messages.Last(message => message.Role == ChatRole.User).Text;
        prompt.ShouldContain("[C1]");
        prompt.ShouldContain("İade ve Para İadesi Politikası");
        prompt.ShouldContain("sürüm 2.0");
        prompt.ShouldContain("yürürlük 2025-06-01");
        prompt.ShouldContain("tür: politika");
        prompt.ShouldContain("2. İade Süresi");
        prompt.ShouldContain("Müşteriler ürünü 30 gün içinde iade edebilir.");
        prompt.ShouldContain("İade süresi kaç gün?");
        prompt.ShouldNotContain("DÜZELTME");
    }

    /// <summary>
    /// Handler'ın düzeltme turunda verdiği geri bildirimin (<c>AnswerFeedback</c>) prompt'a eklendiğini doğrular:
    /// kullanıcı mesajı bir "DÜZELTME" bloğu içerir, kaynak metninde birebir bulunamayan alıntıyı aynen gösterir ve soru
    /// yine mesajın sonunda kalır. Geri bildirim aynı kullanıcı mesajına eklenir; ayrı bir mesaj gönderilmez, çünkü
    /// bazı sohbet şablonları (Gemma dahil) art arda iki kullanıcı mesajını kabul etmez.
    /// </summary>
    /// <remarks>
    /// Sıcaklık 0 ve sabit seed ile aynı istek aynı hatalı alıntıyı yeniden üretirdi; düzeltme turunun işe yaraması için
    /// modelin neyin yanlış olduğunu görmesi gerekir. Bu test kırılırsa ikinci deneme ilkinin kopyası olur ve doğrulanamayan
    /// alıntılar boşuna bir model çağrısından sonra yine reddedilir.
    /// </remarks>
    [Fact]
    public async Task Feedback_about_unverified_quotes_is_added_to_the_prompt()
    {
        var client = new ScriptedChatClient(ValidReply);

        await Create(client).GenerateAsync(
            "İade süresi kaç gün?",
            Context,
            new AnswerFeedback(["İade süresi 900 gündür."]),
            TestContext.Current.CancellationToken);

        var prompt = client.Requests.ShouldHaveSingleItem().Last(message => message.Role == ChatRole.User).Text;
        prompt.ShouldContain("DÜZELTME");
        prompt.ShouldContain("\"İade süresi 900 gündür.\"");
        prompt.ShouldEndWith("SORU: İade süresi kaç gün?");
    }

    /// <summary>
    /// Şemaya uygun JSON yanıtının <c>GeneratedAnswer</c>'a eksiksiz eşlendiğini doğrular: <c>answerable</c>, yanıt
    /// metni, atıflar (<c>chunkId</c> → etiket, alıntı), model adı ve token kullanımı.
    /// </summary>
    /// <remarks>
    /// Model adı olarak sunucunun döndürdüğü kimlik değil yapılandırılan ad raporlanmalıdır: llama.cpp bu alanda yerel
    /// model dosyasının yolunu döndürür ve bu değer API tanılama çıktısına sızarsa makineye ait ayrıntıları açığa
    /// çıkarır. Token sayıları tanılama bilgisine ve soru kaydına taşınır.
    /// </remarks>
    [Fact]
    public async Task The_structured_reply_is_mapped_to_the_generated_answer()
    {
        var answer = await Create(new ScriptedChatClient(ValidReply)).GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken);

        answer.Answerable.ShouldBeTrue();
        answer.Answer.ShouldBe("30 gün içinde iade edebilirsiniz.");
        answer.Citations.ShouldBe([new GeneratedCitation("C1", "30 gün içinde iade edebilir")]);
        answer.Model.ShouldBe("gemma-test"); // sunucunun döndürdüğü dosya yolu değil, yapılandırılan ad
        answer.InputTokens.ShouldBe(100);
        answer.OutputTokens.ShouldBe(20);
    }

    /// <summary>
    /// Yapılandırmadaki örnekleme ayarlarının (<c>Temperature</c> 0, <c>Seed</c> 42, <c>MaxOutputTokens</c> 3000) model
    /// isteğine aynen iletildiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Sıcaklık 0 ve sabit seed yanıtları tekrarlanabilir kılar; değerlendirme sonuçlarının karşılaştırılabilirliği buna
    /// dayanır. <c>MaxOutputTokens</c> bilerek varsayılandan (4096) farklı seçilmiştir: değerin sabit kodlanmış bir
    /// varsayılandan değil yapılandırmadan geldiği böylece kanıtlanır. Düşünme modundaki modellerde akıl yürütme
    /// token'ları sınırı doldurup JSON yanıtına yer bırakmayabilir; bu yüzden sınırın yapılandırılabilir olması gerekir.
    /// </remarks>
    [Fact]
    public async Task Sampling_settings_from_configuration_are_sent_with_the_request()
    {
        var client = new ScriptedChatClient(ValidReply);

        await Create(client, new LlmOptions { ChatModel = "gemma-test", Temperature = 0, Seed = 42, MaxOutputTokens = 3000 })
            .GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken);

        var options = client.Options.ShouldHaveSingleItem().ShouldNotBeNull();
        options.Temperature.ShouldBe(0f);
        options.Seed.ShouldBe(42);
        options.MaxOutputTokens.ShouldBe(3000);
    }

    /// <summary>
    /// İlk yanıt geçerli JSON değilse ("bu json değil") bir kez yeniden denendiğini ve ikinci, geçerli yanıtın
    /// kullanıldığını doğrular (tam olarak iki istek).
    /// </summary>
    /// <remarks>
    /// JSON şemasıyla kısıtlanmış (grammar) üretim normalde geçersiz çıktıyı önler; ancak grammar desteği olmayan
    /// sunucular (<c>UseJsonSchema</c> = false) ya da token sınırında kesilen yanıtlar bozuk JSON üretebilir. Düzeltici
    /// talimatla yapılan tek yeniden deneme bu geçici hataları kullanıcıya yansıtmadan toparlar; denemenin bir ile
    /// sınırlı olması gecikmeyi ve maliyeti öngörülebilir tutar.
    /// </remarks>
    [Fact]
    public async Task An_invalid_reply_is_retried_once()
    {
        var client = new ScriptedChatClient("bu json değil", ValidReply);

        var answer = await Create(client).GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken);

        answer.Answerable.ShouldBeTrue();
        client.Requests.Count.ShouldBe(2);
    }

    /// <summary>
    /// Şemaya uyan ama <c>answerable=true</c> iken <c>answer</c> alanı boş olan yanıtın da yeniden denendiğini ve ikinci
    /// yanıttaki metnin kullanıldığını doğrular.
    /// </summary>
    /// <remarks>
    /// Şema geçerliliği tek başına yeterli değildir: "yanıtlanabilir" deyip yanıt metni vermeyen bir çıktı ne yanıttır ne
    /// de ret. Kabul edilseydi akışın sonraki adımları modelin cümlesi yerine yalnızca alıntı parçalarıyla yetinmek
    /// zorunda kalırdı. Düzeltici talimat bu durumu açıkça adlandırır (answerable=true ise answer boş olmamalı).
    /// </remarks>
    [Fact]
    public async Task A_reply_marked_answerable_without_answer_text_is_retried()
    {
        var client = new ScriptedChatClient(
            """{"answerable":true,"answer":"","citations":[{"chunkId":"C1","quote":"30 gün"}],"missingInformation":"","conflicts":[]}""",
            ValidReply);

        var answer = await Create(client).GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken);

        client.Requests.Count.ShouldBe(2);
        answer.Answer.ShouldBe("30 gün içinde iade edebilirsiniz.");
    }

    /// <summary>
    /// Boş bir JSON nesnesinin (<c>{}</c>) "bilgi yok" kararı sayılmadığını doğrular: zorunlu alanlar eksik olduğu için
    /// yanıt geçersiz çıktıdır, bir kez yeniden denenir ve ikinci <c>{}</c> da <c>InvalidOutput</c> (502) ile sonuçlanır.
    /// </summary>
    /// <remarks>
    /// C# varsayılanlarıyla doldurulan bir nesnede <c>answerable</c> false görünür; bu, modelin kaynakları okuyup verdiği
    /// bir ret değil, bozuk bir çıktıdır. İkisini karıştırmak teknik bir arızayı "dokümanlarda bilgi yok" diye kullanıcıya
    /// gösterir ve değerlendirmede cevapsız soruları haksız yere geçirir. Grammar ile zorlanan şemada bu olası değildir;
    /// şemayı zorlamayan sağlayıcılarda ya da <c>UseJsonSchema=false</c> yapılandırmasında gerçek bir risktir.
    /// </remarks>
    [Fact]
    public async Task An_empty_object_is_invalid_output_not_a_refusal()
    {
        var client = new ScriptedChatClient("{}", "{}");

        var exception = await Should.ThrowAsync<AnswerGenerationException>(
            () => Create(client).GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken));

        exception.Failure.ShouldBe(AnswerGenerationFailure.InvalidOutput);
        client.Requests.Count.ShouldBe(2);
    }

    /// <summary>
    /// Listelerde <c>null</c> öğe bulunan bir yanıtın (atıf, çelişki ya da elenen kimlik) geçersiz çıktı sayılıp bir kez
    /// yeniden denendiğini ve ikinci, geçerli yanıtın kullanıldığını doğrular.
    /// </summary>
    /// <remarks>
    /// Null olamaz işaretleri liste öğelerine uygulanmaz; böyle bir öğe ayrıştırmadan geçer ve ilk kullanıldığı yerde
    /// <c>NullReferenceException</c> ile 500 hatasına dönüşürdü. Düzeltme denemesine yönlendirmek hem kullanıcıya anlamlı
    /// bir sonuç verir hem de arızayı 502/503 ayrımının içinde tutar.
    /// </remarks>
    [Theory]
    [InlineData("""{"answerable":true,"answer":"30 gün.","citations":[null],"missingInformation":"","conflicts":[]}""")]
    [InlineData("""{"answerable":true,"answer":"30 gün.","citations":[{"chunkId":"C1","quote":"30 gün"}],"missingInformation":"","conflicts":[null]}""")]
    [InlineData("""{"answerable":true,"answer":"30 gün.","citations":[{"chunkId":"C1","quote":"30 gün"}],"missingInformation":"","conflicts":[{"topic":"t","chosenChunkId":"C1","rejectedChunkIds":[null],"reason":"r"}]}""")]
    public async Task Null_list_items_are_treated_as_invalid_output(string reply)
    {
        var client = new ScriptedChatClient(reply, ValidReply);

        var answer = await Create(client).GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken);

        client.Requests.Count.ShouldBe(2);
        answer.Answer.ShouldBe("30 gün içinde iade edebilirsiniz.");
    }

    /// <summary>
    /// Tüm alanları dolu, geçerli bir ret yanıtının (<c>answerable=false</c> ve eksik bilgi açıklaması) tek istekte ret
    /// olarak döndüğünü doğrular.
    /// </summary>
    /// <remarks>
    /// Zorunlu alan denetimi bozuk çıktıyı yakalamalı, gerçek retleri değil: model kaynakları yetersiz bulduğunda bu
    /// karar Kapı 2'nin ta kendisidir ve yeniden denenmemelidir.
    /// </remarks>
    [Fact]
    public async Task A_complete_refusal_is_still_a_refusal()
    {
        var client = new ScriptedChatClient(
            """{"answerable":false,"answer":"","citations":[],"missingInformation":"Kaynaklarda bu bilgi yok.","conflicts":[]}""");

        var answer = await Create(client).GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken);

        client.Requests.Count.ShouldBe(1);
        answer.Answerable.ShouldBeFalse();
        answer.MissingInformation.ShouldBe("Kaynaklarda bu bilgi yok.");
    }

    /// <summary>
    /// Modele gönderilen JSON şemasında her alanın zorunlu (<c>required</c>) işaretlendiğini doğrular: kök nesnede
    /// answerable, answer, citations, missingInformation ve conflicts; atıf öğesinde chunkId ve quote; çelişki öğesinde
    /// topic, chosenChunkId, rejectedChunkIds ve reason.
    /// </summary>
    /// <remarks>
    /// llama.cpp şemayı bir grammar'a çevirir ve zorunlu olmayan alanları çıktıda atlanabilir sayar; zorunlu işaretleri
    /// eksik bir şema, modelin <c>{}</c> gibi eksik bir nesne üretmesine izin verirdi. Bu test, ayrıştırma tarafındaki
    /// zorunlu alan denetiminin şema tarafında da karşılığı olduğunu korur.
    /// </remarks>
    [Fact]
    public async Task The_schema_marks_every_field_as_required()
    {
        var client = new ScriptedChatClient(ValidReply);

        await Create(client).GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken);

        var format = client.Options.ShouldHaveSingleItem().ShouldNotBeNull().ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>();
        var schema = format.Schema.ShouldNotBeNull();
        Required(schema).ShouldBe(["answerable", "answer", "citations", "missingInformation", "conflicts"], ignoreOrder: true);
        Required(schema.GetProperty("properties").GetProperty("citations").GetProperty("items")).ShouldBe(["chunkId", "quote"], ignoreOrder: true);
        Required(schema.GetProperty("properties").GetProperty("conflicts").GetProperty("items"))
            .ShouldBe(["topic", "chosenChunkId", "rejectedChunkIds", "reason"], ignoreOrder: true);
    }

    /// <summary>Bir JSON şeması düğümünün <c>required</c> dizisindeki alan adlarını döndürür (dizi yoksa boş).</summary>
    private static string[] Required(System.Text.Json.JsonElement schema) =>
        schema.TryGetProperty("required", out var required)
            ? required.EnumerateArray().Select(name => name.GetString()!).ToArray()
            : [];

    /// <summary>
    /// Art arda iki geçersiz yanıttan sonra yeniden denemeden vazgeçildiğini ve <c>InvalidOutput</c> türünde bir
    /// <c>AnswerGenerationException</c> fırlatıldığını doğrular. API bu durumu 502 (<c>LlmInvalidOutput</c>) olarak
    /// döndürür: sunucu erişilebilir ama kullanılabilir bir yanıt üretemedi. Sınırsız yeniden deneme yerine hızlı ve
    /// anlamlı bir hata tercih edilir.
    /// </summary>
    [Fact]
    public async Task Two_invalid_replies_are_reported_as_invalid_output()
    {
        var generator = Create(new ScriptedChatClient("bozuk", "yine bozuk"));

        var exception = await Should.ThrowAsync<AnswerGenerationException>(
            () => generator.GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken));

        exception.Failure.ShouldBe(AnswerGenerationFailure.InvalidOutput);
    }

    /// <summary>
    /// İstemci katmanındaki taşıma hatasının (<c>HttpRequestException</c>, "connection refused") <c>Unavailable</c>
    /// olarak sınıflandırıldığını doğrular. API bunu 503 (<c>LlmUnavailable</c>) olarak döndürür; böylece istemci ve
    /// operatör "model geçersiz yanıt verdi" (502) ile "modele ulaşılamıyor" (503) durumlarını ayırt edebilir ve ham
    /// istisna 500 olarak dışarı sızmaz.
    /// </summary>
    [Fact]
    public async Task A_transport_failure_is_reported_as_unavailable()
    {
        var client = new ScriptedChatClient(ValidReply) { Failure = new HttpRequestException("connection refused") };

        var exception = await Should.ThrowAsync<AnswerGenerationException>(
            () => Create(client).GenerateAsync("İade süresi kaç gün?", Context, cancellationToken: TestContext.Current.CancellationToken));

        exception.Failure.ShouldBe(AnswerGenerationFailure.Unavailable);
    }
}
