using Knowledge.Application.Abstractions;

namespace SupportAssistant.IntegrationTests.Infrastructure;

/// <summary>
/// API testlerinde dil modelinin yerini alan deterministik sahte üretici: kendisine verilen ilk kaynağı (<c>C1</c>)
/// olduğu gibi hem yanıt metni hem de alıntı olarak döndürür.
/// </summary>
/// <remarks>
/// Entegrasyon testleri modelin kalitesini değil, hattın (arama → Kapı 1 → sürüm çözümü → atıf doğrulama → yanıt zarfı)
/// HTTP üzerinden doğru bağlandığını sınar; bunun için ağa çıkmayan ve her koşuda aynı sonucu veren bir üretici gerekir.
/// Alıntı bölüm metninin kendisi olduğundan <c>CitationValidator</c> atfı geçerli ve doğrulanmış (<c>quoteVerified</c>)
/// sayar. Üretici her zaman yanıt verdiği için bir testte görülen ret modelden değil, sunucu tarafındaki kapılardan
/// gelmiş olmalıdır. Birim testlerindeki ayarlanabilir sahte üreticinin aksine burada ayar yoktur: API testleri tek ve
/// öngörülebilir bir davranışa ihtiyaç duyar.
/// </remarks>
public sealed class FakeAnswerGenerator : IGroundedAnswerGenerator
{
    /// <summary>
    /// Her zaman <c>true</c>: sağlık ucu dil modelini yapılandırılmış raporlar; indeks de hazırsa genel durum <c>ok</c> olur.
    /// </summary>
    public bool IsConfigured => true;

    /// <summary>
    /// Sağlık ucunda ve yanıt teşhisinde (<c>diagnostics.model</c>) görünen ad; gerçek bir modelle karışmasın diye açıkça sahte.
    /// </summary>
    public string ModelName => "fake-llm";

    /// <summary>
    /// İlk bağlam bölümünün içeriğini yanıt ve alıntı olarak döndürür; eksik bilgi, çelişki ve token sayısı bildirmez.
    /// <c>context[0]</c> güvenle kullanılır: handler bağlam boş kaldığında modeli hiç çağırmadan <c>NoSourceInEffect</c>
    /// ile reddeder. <paramref name="feedback"/> yok sayılır: alıntı bölüm metninin kendisi olduğundan her zaman
    /// doğrulanır ve handler bu üreticiyle hiçbir zaman düzeltme turuna girmez.
    /// </summary>
    public Task<GeneratedAnswer> GenerateAsync(
        string question,
        IReadOnlyList<ContextChunk> context,
        AnswerFeedback? feedback = null,
        CancellationToken cancellationToken = default)
    {
        var first = context[0];
        return Task.FromResult(new GeneratedAnswer(
            Answerable: true,
            Answer: first.Chunk.Content,
            Citations: [new GeneratedCitation(first.Label, first.Chunk.Content)],
            MissingInformation: string.Empty,
            Conflicts: [],
            Model: ModelName,
            InputTokens: null,
            OutputTokens: null));
    }
}
