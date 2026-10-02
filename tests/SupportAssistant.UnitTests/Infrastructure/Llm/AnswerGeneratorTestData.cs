using Knowledge.Application.Abstractions;
using Knowledge.Domain.Entities;
using Knowledge.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SupportAssistant.UnitTests.Infrastructure.Llm;

/// <summary>
/// <see cref="OpenAiCompatibleAnswerGenerator"/> testlerinin ortak verisi: geçerli bir model yanıtı, tek bölümlük bir
/// bağlam ve üreticiyi sahte bir sohbet istemcisiyle kuran yardımcı. Test sınıfları bunu <c>using static</c> ile kullanır.
/// </summary>
internal static class AnswerGeneratorTestData
{
    /// <summary>
    /// Şemaya uyan, yanıtlanabilir ve <c>C1</c>'e atıf yapan geçerli bir model yanıtı (JSON); alıntı C1 içeriğinde
    /// birebir geçer.
    /// </summary>
    public const string ValidReply =
        """{"answerable":true,"answer":"30 gün içinde iade edebilirsiniz.","citations":[{"chunkId":"C1","quote":"30 gün içinde iade edebilir"}],"missingInformation":"","conflicts":[]}""";

    /// <summary>
    /// Modele verilen tek bağlam parçası: <c>C1</c> etiketli, iade politikasının 2.0 sürümündeki "2. İade Süresi" bölümü.
    /// Prompt testi etiket, başlık, sürüm, tarih, tür ve bölüm bilgisinin bu kayıttan prompt'a taşındığını kontrol eder.
    /// </summary>
    public static readonly IReadOnlyList<ContextChunk> Context =
    [
        new("C1", new IndexedChunk(Guid.NewGuid(), "iade-v2", "iade", "İade ve Para İadesi Politikası", "2.0", new DateOnly(2025, 6, 1),
            DocumentStatus.Active, DocumentCategory.Policy, "2. İade Süresi", "Müşteriler ürünü 30 gün içinde iade edebilir."))
    ];

    /// <summary>
    /// Test edilen üreticiyi verilen sahte istemciyle kurar. Seçenek verilmezse yalnızca <c>ChatModel</c> = "gemma-test"
    /// ayarlanır; bu ad, raporlanan model adının sunucudan değil yapılandırmadan geldiğini kanıtlamak için kullanılır.
    /// </summary>
    public static OpenAiCompatibleAnswerGenerator Create(IChatClient client, LlmOptions? options = null) =>
        new(client, Options.Create(options ?? new LlmOptions { ChatModel = "gemma-test" }), NullLogger<OpenAiCompatibleAnswerGenerator>.Instance);
}
