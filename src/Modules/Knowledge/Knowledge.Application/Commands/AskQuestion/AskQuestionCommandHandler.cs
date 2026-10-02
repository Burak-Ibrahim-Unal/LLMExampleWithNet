using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Application.Exceptions;
using Knowledge.Application.Options;
using Knowledge.Application.Security;
using Knowledge.Domain.Entities;
using Knowledge.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Application.Common;

namespace Knowledge.Application.Commands.AskQuestion;

/// <summary>
/// Soru-cevap hattının kalbi: bir soruyu yalnızca bilgi tabanındaki, bugün yürürlükte olan kaynaklara dayanarak
/// yanıtlar ya da "dokümanlarda yeterli bilgi yok" diyerek açıkça reddeder. Adımlar sırasıyla:
/// iş kuralları (boş soru, 500 karakter sınırı, indeksin hazır olması) → prompt injection denetimi
/// (<see cref="PromptInjectionDetector"/>; yakalanan soru aramaya ve modele ulaşmaz) → hibrit arama (BM25 + varsa vektör
/// benzerliği, RRF ile birleştirilir) → Kapı 1 (<see cref="AnswerabilityPolicy"/>: bilgi tabanında soruya yeterince yakın
/// bir şey var mı?) → sürüm çözümleme (<see cref="VersionResolver"/>: her doküman ailesinde yalnızca yürürlükteki sürüm
/// kalır, sadece eski sürüm eşleştiyse güncel sürümün bölümleri onun yerine konur, yürürlükte kaynak kalmazsa
/// <c>NoSourceInEffect</c> reddi) → dil modeli → çıktı koruması (sistem prompt'unu tekrarlayan yanıt
/// <c>UnsafeOutput</c> ile reddedilir) → Kapı 2 (modelin kendi <c>answerable</c> kararı) → Kapı 3
/// (<see cref="CitationValidator"/>: yanıt, modele verilen bir bölümde birebir geçen en az bir alıntıya dayanıyor mu?)
/// → kaynaklar arası çelişkide öncelik kuralının zorlanması (<see cref="SourcePrecedence"/>) → yanıt metninin
/// temizlenmesi (<see cref="AnswerText"/>) → <see cref="QuestionLog"/> denetim kaydı.
/// </summary>
/// <remarks>
/// <para>
/// Her kapı yanıtı durdurabilir. Durdurulan yanıt bir hata değil, HTTP 200 ile dönen açık bir "bilmiyorum"dur:
/// <c>answerable=false</c>, sabit Türkçe mesaj, boş <c>sources</c> ve hangi kapıda durulduğunu söyleyen
/// <c>refusalReason</c>. Görevin "dokümanlarda yeterli bilgi yoksa yanıt üretme, bunu açıkça söyle" şartı böyle
/// karşılanır. Ucuz ve deterministik adımlar (iş kuralları, Kapı 1, sürüm çözümleme) bilerek pahalı ve deterministik
/// olmayan LLM çağrısından önce gelir: alakasız ya da yalnızca eski sürümlere dayanan sorularda model hiç çağrılmaz ve
/// model eski bir kuralı hiçbir zaman görmez. Modelden sonraki kontroller ise model çıktısına körü körüne güvenmemek
/// içindir. Böylece kararların çoğu prompt'a bırakılmaz, birim testleriyle doğrulanabilen kodda verilir.
/// </para>
/// <para>
/// Model yanıtı iki nedenle kabul edilmeyebilir: hiçbir alıntı atıf yapılan bölümde doğrulanamaz ya da model bir
/// çelişkide öncelik kuralını çiğner (kaybeden kaynağı seçer, yanıtını ona dayandırır, kuralın kazananına hiç atıf
/// yapmaz ya da çelişkiyi verilen kaynaklarda olmayan kimliklerle bildirir). İkisi de düzeltilebilir hatalardır; bu yüzden hemen reddetmek yerine model bir kez daha çağrılır:
/// doğrulanamayan alıntılar geri bildirim olarak iletilir, kurala göre kaybeden bölümler bağlamdan çıkarılır. Düzeltme
/// turundan sonra da kabul edilmeyen yanıt <c>NoValidCitations</c> ya da <c>UnresolvedConflict</c> ile reddedilir. Soru
/// başına en fazla iki gerçek model isteği yapılır; üreticinin şema yeniden denemesi de bu bütçeden düşer.
/// </para>
/// </remarks>
public sealed class AskQuestionCommandHandler(
    IKnowledgeIndex index,
    IGroundedAnswerGenerator generator,
    KnowledgeBusinessRules rules,
    AnswerabilityPolicy answerabilityPolicy,
    VersionResolver versionResolver,
    IQuestionLogRepository questionLogs,
    IOptions<RetrievalOptions> options,
    ILogger<AskQuestionCommandHandler> logger) : IRequestHandler<AskQuestionCommand, ApiResult<AnswerDto>>
{
    /// <summary>
    /// Bir ailede soruyla yalnızca eski bir sürüm eşleştiğinde, yürürlükteki sürümden eski bölümlerin yerine getirilen
    /// en iyi bölüm sayısı. Küçük tutulur: amaç güncel kuralın soruyla ilgili bölümünü bağlama sokmaktır; bağlamı
    /// (<c>TopK</c>) güncel sürümün ilgisiz bölümleriyle doldurmak değil.
    /// </summary>
    private const int SubstituteSectionCount = 2;

    /// <summary>
    /// Soru başına en fazla gerçek model isteği; üreticinin geçersiz çıktı için yaptığı yeniden deneme de bu bütçeden
    /// düşer. Yerel model her çağrıda saniyeler harcadığından sınır bilinçli olarak düşük tutulur: tipik akış tek
    /// istektir, ikinci istek ya üreticinin şema düzeltmesine ya da handler'ın düzeltme turuna harcanır. Bütçe bittiğinde
    /// kabul edilmeyen yanıt açıkça reddedilir; alıntı ve öncelik düzeltmeleri gerekirse aynı istekte birlikte uygulanır.
    /// </summary>
    /// <remarks>
    /// Bütçe tek bir yerde tutulur ve üreticiye her çağrıda yalnızca kalanı verilir. Önceden handler'ın iki denemesi ile
    /// üreticinin iki denemesi çarpılıp sunucuya dört istek gidebiliyor, tanılama ise iki diyordu.
    /// </remarks>
    private const int MaxModelCalls = 2;

    /// <summary>
    /// Denetim kaydındaki yanıt JSON'u (<c>QuestionLog.ResponseJson</c>) için serileştirme ayarları. Web varsayılanları
    /// alan adlarını API yanıtındaki gibi camelCase yazar; <c>UnsafeRelaxedJsonEscaping</c> ise Türkçe karakterlerin
    /// (ç, ğ, ı, ö, ş, ü) Unicode kaçış dizileri olarak yazılmasını önler, böylece kayıt veritabanında doğrudan
    /// okunabilir. Adındaki "unsafe" uyarısı JSON'un HTML içine gömüldüğü durumlar içindir; bu metin yalnızca
    /// veritabanında saklanır.
    /// </summary>
    private static readonly JsonSerializerOptions LogJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Soru-cevap hattının tamamını çalıştırır ve sonucu <c>ApiResult</c> zarfıyla döndürür. Yanıt da ret de 200'dür;
    /// hatalar kendi durum koduyla döner: geçersiz soru 400, hazır olmayan indeks 503, erişilemeyen dil modeli 503,
    /// şemaya uymayan model çıktısı 502.
    /// </summary>
    /// <remarks>
    /// Yanıtlar ve retler <see cref="QuestionLog"/> tablosuna yazılır. İş kuralı hataları ve dil modeli hataları
    /// (502/503) ise yazılmaz, çünkü ortada değerlendirilecek bir yanıt yoktur; model hataları yalnızca uyarı olarak
    /// loglanır. Kronometre iş kurallarından sonra başlar; tanılamadaki gecikme, aramadan tanılamanın oluşturulduğu ana
    /// kadar geçen süredir (model çağrıldıysa düzeltme turu dahil tüm çağrıları kapsar).
    /// </remarks>
    public async Task<ApiResult<AnswerDto>> Handle(AskQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = request.Question?.Trim() ?? string.Empty;

        // İş kuralları: başarısız olan ilk kural, arama ve model maliyetine girmeden hata zarfı olarak döner.
        var requiredError = rules.CheckQuestionRequired<AnswerDto>(question);
        if (requiredError is not null)
        {
            return requiredError;
        }

        var lengthError = rules.CheckQuestionLength<AnswerDto>(question);
        if (lengthError is not null)
        {
            return lengthError;
        }

        var readyError = rules.CheckIndexReady<AnswerDto>();
        if (readyError is not null)
        {
            return readyError;
        }

        var stopwatch = Stopwatch.StartNew();
        var settings = options.Value;

        // Prompt injection: talimatları değiştirmeye yönelik bir soru aramaya ve modele hiç ulaşmaz. Hangi kalıbın
        // yakalandığı yalnızca loga yazılır; istemci genel bir ret mesajı alır, olay denetim kaydında görünür.
        if (PromptInjectionDetector.Detect(question) is { } injectionRule)
        {
            logger.LogWarning("Question refused as a suspected prompt injection ({Rule}); the model was not called.", injectionRule);
            var noRetrieval = new AnswerDiagnosticsDto(index.Status.Mode.ToApi(), 0, 0, [], [], string.Empty, stopwatch.ElapsedMilliseconds, null, null, 0);
            return await RefuseAsync(
                question,
                RefusalReasons.PromptInjectionSuspected,
                string.Empty,
                noRetrieval,
                cancellationToken,
                Messages.Knowledge.PromptInjectionRefused);
        }

        // Hibrit arama. Modelin göreceğinden fazlası (TopK * 2) getirilir: eski sürümlerin bölümleri bir sonraki adımda
        // elenecek, bağlam yine de dolu kalmalı. Sorgu bir kez hazırlanır (gerekirse embed edilir) ve yedek bölüm
        // aramalarında yeniden kullanılır.
        var prepared = await index.PrepareAsync(question, cancellationToken);
        var retrieval = index.Search(prepared, settings.TopK * 2);
        var candidateDocumentIds = retrieval.Hits.Select(hit => hit.Chunk.DocumentId).Distinct().ToList();

        // Kapı 1: en iyi kosinüs benzerliği de en iyi sözcük kapsamı da eşiğin altındaysa model hiç çağrılmadan reddedilir.
        // LLM maliyeti doğmaz ve alan dışı sorularda modelin "yardımsever" bir uydurma yapma riski ortadan kalkar.
        if (!answerabilityPolicy.HasEnoughEvidence(retrieval))
        {
            return await RefuseAsync(question, RefusalReasons.LowRelevance, string.Empty, Diagnostics(retrieval, candidateDocumentIds, [], string.Empty, stopwatch, null), cancellationToken);
        }

        // Sürüm çözümleme prompt'ta değil kodda yapılır: her ailede yalnızca yürürlükteki sürüm kalır, sadece eski sürüm
        // eşleştiyse güncel sürümün en iyi bölümleri onun yerine konur. Bağlam TopK ile kesilir ve C1..Cn diye
        // etiketlenir; model kaynaklara bu etiketlerle atıf yapar.
        var resolution = versionResolver.Resolve(retrieval.Hits, index.GetDocumentVersions);
        var context = Label(WithSubstitutes(retrieval.Hits, resolution, prepared).Take(settings.TopK).Select(hit => hit.Chunk));

        // İlgili her şey yürürlükten kalkmış ya da henüz yürürlüğe girmemiş: modelin kullanabileceği bir kaynak yok.
        // Eski kuralı modele göstermek yerine model çağrılmadan NoSourceInEffect gerekçesiyle reddedilir.
        if (context.Count == 0)
        {
            return await RefuseAsync(question, RefusalReasons.NoSourceInEffect, string.Empty, Diagnostics(retrieval, candidateDocumentIds, context, string.Empty, stopwatch, null), cancellationToken);
        }

        var usage = new ModelUsage();
        AnswerFeedback? feedback = null;
        var enforcedConflicts = new List<ConflictDto>();

        // Döngünün koşulu yoktur: her tur ya bir yanıt ya bir ret döndürür ya da düzeltme turuna geçer. Bütün turlar
        // birlikte en fazla MaxModelCalls gerçek model isteği yapabilir.
        while (true)
        {
            // Dil modeli çağrısı; üreticiye yalnızca kalan bütçe verilir. Buradaki hatalar "bilgi yok" reddi değil gerçek
            // hatalardır: erişilemeyen sunucu 503, üreticinin bütçesi içinde şemaya uymayan çıktı 502 olarak döner.
            GeneratedAnswer generated;

            try
            {
                generated = await generator.GenerateAsync(question, context, feedback, MaxModelCalls - usage.Calls, cancellationToken);
            }
            catch (AnswerGenerationException exception)
            {
                logger.LogWarning(exception, "Answer generation failed ({Failure}).", exception.Failure);

                return exception.Failure == AnswerGenerationFailure.Unavailable
                    ? ApiResult<AnswerDto>.Fail(Messages.Knowledge.LlmUnavailable, 503)
                    : ApiResult<AnswerDto>.Fail(Messages.Knowledge.LlmInvalidOutput, 502);
            }

            usage.Add(generated);
            var diagnostics = Diagnostics(retrieval, candidateDocumentIds, context, generated.Model, stopwatch, usage);

            // Çıktı koruması: model sistem prompt'unu tekrarladıysa yanıt, doğrulanmış atfı olsa bile gösterilmez. Böyle bir
            // çıktı bir manipülasyonun (kaynağa gömülü talimat ya da ustaca kurulmuş soru) işe yaradığını gösterir; aynı
            // bağlamla yeniden denemek aynı sonucu verebileceği için düzeltme turu yapılmaz. Sızıntı eksik bilgi
            // açıklamasında da olabileceğinden modelin hiçbir metni istemciye dönmez.
            if (generated.LeaksSystemPrompt)
            {
                logger.LogWarning("The model's output repeated the system prompt; the answer was withheld.");
                return await RefuseAsync(
                    question,
                    RefusalReasons.UnsafeOutput,
                    string.Empty,
                    diagnostics,
                    cancellationToken,
                    Messages.Knowledge.UnsafeOutputRefused);
            }

            // Kapı 2: model verilen kaynakları yetersiz bulduysa yanıt uydurmaz; neyin eksik olduğunu belirterek reddeder.
            // Bu geçerli bir karardır, düzeltme turu gerektirmez.
            if (!generated.Answerable)
            {
                return await RefuseAsync(question, RefusalReasons.ModelInsufficientContext, generated.MissingInformation, diagnostics, cancellationToken);
            }

            // Kapı 3: yalnızca modele verilen C1..Cn etiketlerinden birine işaret eden ve alıntısı o bölümde birebir geçen
            // atıflar yanıtın dayanağı olur. Doğrulanamayan alıntı, kaynaklı görünen ama kaynakta olmayan bir iddiadır.
            var citations = CitationValidator.Validate(generated.Citations, context);
            var accepted = citations.Where(citation => citation.QuoteVerified).ToList();

            // Öncelik kuralı: model bir çelişkide kurala göre kaybeden kaynağı seçtiyse ya da yanıtını kaybeden bir kaynağa
            // dayandırdıysa eski ya da daha az yetkili bilgi müşteriye ulaşırdı. Kuralın kazananına hiç atıf yapılmaması da
            // ihlaldir: çelişki kaydı "politikayı seçtim" derken yanıt başka bir kaynağa dayanıyorsa beyan ile yanıt
            // birbirini tutmaz. Verilen kaynaklarda olmayan kimliklerle bildirilen çelişkiler denetlenemez; onlar da
            // yutulmaz, düzeltme turuna gider.
            var (conflicts, invalidConflictReferences) = CheckConflicts(generated.Conflicts, context);
            var losers = conflicts.SelectMany(conflict => conflict.Losers).ToHashSet();
            var citedDocuments = accepted.Select(citation => citation.Source.Chunk.DocumentId).ToHashSet(StringComparer.Ordinal);
            var violatesPrecedence = conflicts.Any(conflict => !conflict.RuleSatisfied || !citedDocuments.Contains(conflict.Winner.Chunk.DocumentId))
                || accepted.Any(citation => losers.Contains(citation.Source));
            var hasInvalidConflictReferences = invalidConflictReferences > 0;

            if (accepted.Count == 0 || violatesPrecedence || hasInvalidConflictReferences)
            {
                // Bütçe bittiyse (üreticinin şema düzeltmesi ikinci isteği harcadıysa da) düzeltme turu yapılamaz.
                if (usage.Calls >= MaxModelCalls)
                {
                    var reason = accepted.Count == 0 ? RefusalReasons.NoValidCitations : RefusalReasons.UnresolvedConflict;
                    return await RefuseAsync(question, reason, generated.MissingInformation, diagnostics, cancellationToken);
                }

                logger.LogWarning(
                    "The answer was not accepted (verified citations: {VerifiedCitations}, precedence violated or conflict winner not cited: {PrecedenceViolated}, conflicts with unknown labels: {InvalidConflicts}); asking the model once more.",
                    accepted.Count,
                    violatesPrecedence,
                    invalidConflictReferences);

                // Düzeltme turu: doğrulanamayan alıntılar ve geçersiz çelişki kimlikleri modele geri bildirim olarak
                // gösterilir; öncelik ihlalinde kurala göre kaybeden bölümler bağlamdan çıkarılır ve bağlam yeniden C1..Cn
                // diye etiketlenir. Sunucunun kararı çelişki kaydı olarak saklanır, çünkü kaybeden kaynak artık bağlamda
                // olmadığından model çelişkiyi bir daha bildiremez.
                feedback = accepted.Count == 0 || hasInvalidConflictReferences
                    ? new AnswerFeedback(
                        accepted.Count == 0 ? citations.Where(citation => !citation.QuoteVerified).Select(citation => citation.Quote).ToList() : [],
                        CitationsRejected: accepted.Count == 0,
                        InvalidConflictReferences: hasInvalidConflictReferences)
                    : null;

                if (violatesPrecedence)
                {
                    enforcedConflicts.AddRange(conflicts.Select(conflict => conflict.ToEnforcedDto()));
                    context = Label(context.Where(source => !losers.Contains(source)).Select(source => source.Chunk));
                }

                continue;
            }

            if (accepted.Count < citations.Count)
            {
                logger.LogInformation(
                    "{Count} citation(s) whose quote was not found in the cited section were left out of the sources.",
                    citations.Count - accepted.Count);
            }

            // Model talimatlara rağmen metne "[C1]" gibi kaynak işaretleri ya da alıntı kopyaları bırakabilir. Kaynaklar kendi
            // alanında taşındığı için bunlar temizlenir; yanıt müşteriye temiz okunmalıdır.
            var answerText = AnswerText.Clean(generated.Answer, citations.Select(citation => citation.Quote).ToList());

            // Modelin metni yalnızca kaynak işaretlerinden ya da alıntılardan oluşuyorsa temizlikten sonra boş kalır; bu durumda
            // doğrulanmış alıntıların kendisi yanıt olur. Doğrulanmış alıntı boş olamayacağından yanıt metni de boş kalmaz.
            if (string.IsNullOrWhiteSpace(answerText))
            {
                answerText = string.Join(" ", accepted.Select(citation => citation.Quote).Distinct());
            }

            // Sürüm kararları yalnızca yanıtın gerçekten dayandığı (doğrulanmış atıf yapılan) doküman aileleri için
            // raporlanır; bağlama girip yanıtta kullanılmayan bir ailenin kararı kullanıcıyı yanlış dokümana yönlendirirdi.
            var citedFamilies = accepted.Select(citation => citation.Source.Chunk.DocumentKey).ToHashSet(StringComparer.Ordinal);

            // Çelişki kayıtları için de aynı ilke geçerlidir: düzeltme turundan önce saklanan sunucu kararları dahil, yalnızca
            // seçilen kaynağı yanıtın atıf yaptığı dokümanlar arasında olan kayıtlar gösterilir.
            var reportedConflicts = enforcedConflicts
                .Concat(conflicts.Select(conflict => conflict.ToDto()))
                .Where(conflict => citedDocuments.Contains(conflict.Chosen.DocumentId))
                .ToList();

            var answer = new AnswerDto(
                question,
                Answerable: true,
                Answer: answerText,
                Sources: accepted.Select(ToSourceDto).ToList(),
                VersionResolution: ToDto(resolution, citedFamilies),
                Conflicts: reportedConflicts,
                MissingInformation: generated.MissingInformation.Trim(),
                RefusalReason: string.Empty,
                Diagnostics: diagnostics);

            await LogAsync(answer, cancellationToken);
            return ApiResult<AnswerDto>.Ok(answer);
        }
    }

    /// <summary>
    /// Açık bir ret yanıtı oluşturur, denetim kaydına yazar ve HTTP 200 ile döndürür. <paramref name="reason"/> hangi
    /// kapıda durulduğunu söyleyen <see cref="RefusalReasons"/> değeridir; <paramref name="missingInformation"/> modelin
    /// neyin eksik olduğuna dair açıklamasıdır (model çağrılmadıysa boş).
    /// </summary>
    /// <remarks>
    /// Ret bir hata değil geçerli bir iş sonucudur: istemci <c>answerable=false</c>, boş <c>sources</c> ve yanıt alanında
    /// sabit bir Türkçe mesaj alır; modelin ürettiği metin retlerde hiçbir zaman yanıt gibi gösterilmez. Mesaj varsayılan
    /// olarak "dokümanlarda yeterli bilgi yok" cümlesidir; sorun dokümanlarda değil sorunun kendisinde (prompt injection)
    /// ya da modelin çıktısında (çıktı koruması) olduğunda buna özel mesaj verilir. Retler sürüm kararı taşımaz (yalnızca kural metni, boş listeler): sürüm kararları
    /// bir yanıtın kaynaklarını açıklamak içindir, yanıt yoksa açıklanacak kaynak da yoktur. Tanılama ise bilerek
    /// doldurulur; neyin bulunduğu ve modele neyin gösterildiği görülebilsin, "neden reddedildi?" sorusu yanıttan ve
    /// denetim kaydından cevaplanabilsin.
    /// </remarks>
    /// <param name="question">Reddedilen soru.</param>
    /// <param name="reason">Ret nedeni (<see cref="RefusalReasons"/>).</param>
    /// <param name="missingInformation">Modelin eksik bilgi açıklaması; yoksa boş.</param>
    /// <param name="diagnostics">Yanıtta ve denetim kaydında gösterilecek tanılama.</param>
    /// <param name="cancellationToken">İsteğin iptal belirteci.</param>
    /// <param name="message">Yanıt metni ve zarf mesajı; null ise <c>Messages.Knowledge.NotEnoughInformation</c>.</param>
    private async Task<ApiResult<AnswerDto>> RefuseAsync(
        string question,
        string reason,
        string missingInformation,
        AnswerDiagnosticsDto diagnostics,
        CancellationToken cancellationToken,
        string? message = null)
    {
        var text = message ?? Messages.Knowledge.NotEnoughInformation;
        var refusal = new AnswerDto(
            question,
            Answerable: false,
            Answer: text,
            Sources: [],
            VersionResolution: new VersionResolutionDto(false, VersionResolver.Rule, [], []),
            Conflicts: [],
            MissingInformation: missingInformation.Trim(),
            RefusalReason: reason,
            Diagnostics: diagnostics);

        await LogAsync(refusal, cancellationToken);
        return ApiResult<AnswerDto>.Ok(refusal, text);
    }

    /// <summary>
    /// Sürüm çözümlemesinin sonucunu, arama sırasını koruyarak bağlam adaylarına çevirir. Yürürlükteki sürümlerin
    /// bulunan bölümleri yerinde kalır. Bir ailede yalnızca eski sürüm eşleşmiş, yürürlükteki sürüm hiç bulunmamışsa,
    /// yürürlükteki sürümün soruya en uygun (en fazla <see cref="SubstituteSectionCount"/>) bölümü o ailenin ilk eski
    /// bölümünün yerine konur. Diğer elenen bölümler (eski sürümlerin kalan bölümleri, yürürlükte sürümü olmayan aileler) listeye
    /// girmez.
    /// </summary>
    /// <remarks>
    /// Yerine koyma olmasaydı, sözcükleri eski sürümle daha iyi örtüşen bir soru, eski bölüm elendikten sonra güncel
    /// kuraldan tek bir bölüm görmeden kalır ve haksız yere reddedilirdi. Yedek bölümler aynı hazırlanmış sorguyla,
    /// yalnızca o dokümana süzülmüş bir aramayla bulunur; sorgu yeniden embed edilmez. Yedekler eski bölümün arama
    /// sırasındaki yerini devraldığı için, bağlam ardından <c>TopK</c> ile kesildiğinde soruyla ne kadar ilgili
    /// bulunduysa o konumda yer alırlar.
    /// </remarks>
    private List<SearchHit> WithSubstitutes(IReadOnlyList<SearchHit> candidates, VersionResolution resolution, PreparedQuery query)
    {
        var substitutes = resolution.NeedsSubstitution.ToDictionary(
            version => version.DocumentKey,
            version => index.Search(query, SubstituteSectionCount, chunk => chunk.DocumentId == version.DocumentId).Hits);

        var kept = resolution.Kept.Select(hit => hit.Chunk.ChunkId).ToHashSet();
        var merged = new List<SearchHit>();

        foreach (var hit in candidates)
        {
            if (kept.Contains(hit.Chunk.ChunkId))
            {
                merged.Add(hit);
            }
            else if (substitutes.Remove(hit.Chunk.DocumentKey, out var replacement))
            {
                // Remove sayesinde yedekler aile başına yalnızca bir kez, o ailenin ilk eski bölümünün yerine eklenir.
                merged.AddRange(replacement);
            }
        }

        return merged;
    }

    /// <summary>
    /// Bölümleri sırayla C1..Cn diye etiketleyerek modele verilecek bağlamı kurar.
    /// </summary>
    /// <remarks>
    /// Hem ilk bağlam hem de çelişki düzeltmesinde kaybeden bölümler çıkarıldıktan sonraki bağlam bununla etiketlenir:
    /// etiketler her istekte boşluksuz ve sıralı kalır, model böylece yalnızca o istekte gördüğü etiketlere atıf yapar.
    /// </remarks>
    private static List<ContextChunk> Label(IEnumerable<IndexedChunk> chunks) =>
        chunks.Select((chunk, position) => new ContextChunk($"C{position + 1}", chunk)).ToList();

    /// <summary>
    /// Modelin bildirdiği, farklı dokümanlar arasındaki çelişkileri sunucu tarafında doğrular. Her çelişki için kurala
    /// (<see cref="SourcePrecedence"/>: politika/prosedür &gt; kılavuz &gt; SSS, eşit yetkide daha yeni yürürlük tarihi)
    /// göre kaybeden kaynaklar ve modelin seçiminin kurala uyup uymadığı hesaplanır. Verilen bağlamda olmayan kimlikler
    /// ayrıca sayılır.
    /// </summary>
    /// <remarks>
    /// Görev, kaynaklar çeliştiğinde güncel olanın nasıl seçildiğinin gösterilmesini istiyor; bunu yalnızca modelin
    /// beyanına bırakmak, modelin yanlış kaynağı seçtiği durumları gizlerdi. Model etiketleri farklı biçimlerde
    /// yazabildiği için ("c2", "[C2]", "2") etiketler önce normalize edilir, seçilen kaynak reddedilenler arasında sayılmaz
    /// ve tekrarlar ayıklanır. Bağlamda olmayan bir kimlik (seçilen ya da elenen) sessizce yutulmaz: çelişki geçersiz
    /// kimlik içerir diye sayılır ve handler bunu düzeltme turuna, sürerse <c>UnresolvedConflict</c> reddine götürür. Seçilen
    /// kaynağı ve en az bir elenen kaynağı bağlamda olan çelişkiler yine de denetlenir; kuralı zorlamak için bu kısım
    /// yeterlidir. Aynı belgenin sürümleri arasındaki seçim burada değil, model çağrılmadan önce
    /// <see cref="VersionResolver"/> tarafından yapılır.
    /// </remarks>
    /// <param name="conflicts">Modelin bildirdiği çelişkiler.</param>
    /// <param name="context">Modele bu denemede verilen etiketli bölümler.</param>
    /// <returns>Denetlenebilen çelişkiler ve geçersiz kimlik içeren çelişki sayısı.</returns>
    private static (IReadOnlyList<CheckedConflict> Conflicts, int InvalidReferences) CheckConflicts(IReadOnlyList<GeneratedConflict> conflicts, IReadOnlyList<ContextChunk> context)
    {
        var sourcesByLabel = context.ToDictionary(source => source.Label, StringComparer.OrdinalIgnoreCase);
        var checkedConflicts = new List<CheckedConflict>();
        var invalidReferences = 0;

        foreach (var conflict in conflicts)
        {
            if (!sourcesByLabel.TryGetValue(SourceLabel.Normalize(conflict.ChosenChunkLabel), out var chosen))
            {
                invalidReferences++;
                continue;
            }

            var rejectedLabels = conflict.RejectedChunkLabels
                .Select(SourceLabel.Normalize)
                .Distinct()
                .Where(label => label != chosen.Label)
                .ToList();
            var rejected = rejectedLabels
                .Where(sourcesByLabel.ContainsKey)
                .Select(label => sourcesByLabel[label])
                .ToList();

            if (rejected.Count < rejectedLabels.Count || rejected.Count == 0)
            {
                invalidReferences++;
            }

            if (rejected.Count > 0)
            {
                checkedConflicts.Add(new CheckedConflict(conflict.Topic.Trim(), chosen, rejected, conflict.Reason.Trim()));
            }
        }

        return (checkedConflicts, invalidReferences);
    }

    /// <summary>
    /// Yanıtlanan ya da reddedilen her soruyu, istemciye dönen yanıtın tam JSON kopyasıyla birlikte
    /// <see cref="QuestionLog"/> tablosuna yazar.
    /// </summary>
    /// <remarks>
    /// Denetim ve değerlendirme içindir: hangi sorunun hangi kapıda durduğu, hangi modelin ne kadar sürede yanıt verdiği
    /// ve istemciye tam olarak ne döndüğü sonradan incelenebilir. Filtrelemeye uygun özet alanlar (yanıtlanabilirlik, ret
    /// nedeni, model, gecikme) ayrı sütunlardadır; ayrıntının tamamı <c>ResponseJson</c> içindedir.
    /// </remarks>
    private async Task LogAsync(AnswerDto answer, CancellationToken cancellationToken)
    {
        var log = new QuestionLog(
            answer.Question,
            answer.Answerable,
            answer.RefusalReason,
            answer.Diagnostics.Model,
            answer.Diagnostics.LatencyMs,
            JsonSerializer.Serialize(answer, LogJsonOptions));

        await questionLogs.AddAsync(log, cancellationToken);
        await questionLogs.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// <see cref="VersionResolution"/> sonucunu yanıttaki <c>versionResolution</c> alanına çevirir; yalnızca
    /// <paramref name="relevantFamilies"/> içindeki, yani yanıtın atıf yaptığı doküman ailelerinin seçilen ve elenen
    /// sürümlerini, elenme gerekçeleriyle birlikte taşır. <c>Applied</c>, bu süzmeden sonra en az bir sürüm elenmişse
    /// true olur.
    /// </summary>
    /// <remarks>
    /// Görevin "kaynaklar çeliştiğinde güncel sürümün nasıl seçildiğini göster" şartı bu alanla karşılanır. Süzme
    /// olmasaydı, örneğin kargo ücreti sorusunda bağlama giren ama yanıtta kullanılmayan iade politikasının sürüm kararı
    /// da raporlanır ve kullanıcı yanıtın yanlış bir dokümana dayandığını düşünebilirdi. Kural metni
    /// (<see cref="VersionResolver.Rule"/>) her zaman eklenir; seçimin hangi kurala göre yapıldığı yanıtın içinden okunur.
    /// </remarks>
    private static VersionResolutionDto ToDto(VersionResolution resolution, IReadOnlySet<string> relevantFamilies)
    {
        var discarded = resolution.Discarded
            .Where(item => relevantFamilies.Contains(item.Version.DocumentKey))
            .Select(item => new DiscardedVersionDto(item.Version.DocumentId, item.Version.Title, item.Version.Version, item.Version.EffectiveDate, item.Reason))
            .ToList();

        var selected = resolution.Selected
            .Where(version => relevantFamilies.Contains(version.DocumentKey))
            .Select(version => new VersionRefDto(version.DocumentId, version.Title, version.Version, version.EffectiveDate))
            .ToList();

        return new VersionResolutionDto(discarded.Count > 0, VersionResolver.Rule, selected, discarded);
    }

    /// <summary>
    /// Kabul edilmiş (etiketi bağlamda, alıntısı doğrulanmış) bir atıfı yanıttaki <c>sources</c> öğesine çevirir: doküman
    /// kimliği, başlık, sürüm, yürürlük tarihi, durum, tür, bölüm yolu, alıntı ve <c>quoteVerified</c>. Görevin "her yanıt
    /// kullandığı dokümanı ve ilgili bölümü göstermeli" şartı bu alanlarla karşılanır. Yalnızca doğrulanmış atıflar kaynak
    /// olduğundan <c>quoteVerified</c> burada her zaman true'dur; alan, sözleşmenin açık kalması ve istemcinin bunu
    /// kendisi de denetleyebilmesi için taşınır.
    /// </summary>
    private static AnswerSourceDto ToSourceDto(ValidatedCitation citation)
    {
        var chunk = citation.Source.Chunk;
        return new AnswerSourceDto(
            chunk.DocumentId,
            chunk.Title,
            chunk.Version,
            chunk.EffectiveDate,
            chunk.Status.ToApi(),
            chunk.Category.ToApi(),
            chunk.SectionPath,
            citation.Quote,
            citation.QuoteVerified);
    }

    /// <summary>
    /// Bir çelişkide seçilen ya da reddedilen kaynağı, öncelik kararını denetlemeye yetecek alanlarla (doküman, sürüm,
    /// yürürlük tarihi, tür, bölüm) temsil eder. Tür ve tarih bilerek dahil edilir: <c>RuleSatisfied</c> kararı tam da bu
    /// iki alana dayanır ve okuyan kişi kararı kendisi de kontrol edebilmelidir.
    /// </summary>
    private static ConflictSourceDto ToConflictSource(ContextChunk source) =>
        new(source.Chunk.DocumentId, source.Chunk.Version, source.Chunk.EffectiveDate, source.Chunk.Category.ToApi(), source.Chunk.SectionPath);

    /// <summary>
    /// Yanıtın <c>diagnostics</c> bölümünü oluşturur: arama modu (hybrid/lexical), Kapı 1'in baktığı iki sinyal (en iyi
    /// kosinüs benzerliği ve en iyi sözcük kapsamı), sürüm çözümlemesinden önce bulunan doküman kimlikleri, modele
    /// etiketleriyle verilen bölümler, model adı, gecikme, model çağrı sayısı ve varsa toplam token sayıları.
    /// </summary>
    /// <remarks>
    /// Hem yanıtlarda hem retlerde doldurulur; "neden reddedildi, neden bu kaynak?" soruları ancak bu verilerle
    /// cevaplanabilir. Model adı, adaptörün döndürdüğü <c>GeneratedAnswer.Model</c> değeridir, yani yapılandırılmış model
    /// adıdır (llama.cpp'nin bildirdiği kimlik yerel model dosyasının yolunu içerdiği için kullanılmaz); model çağrılmadan
    /// verilen retlerde boştur. <paramref name="usage"/> da bu yüzden null olabilir; o durumda çağrı sayısı 0'dır ve token
    /// sayısı yoktur. Gecikme, kronometrenin başladığı andan (iş kurallarından sonra) bu metodun çağrıldığı ana kadar geçen
    /// süredir.
    /// </remarks>
    private static AnswerDiagnosticsDto Diagnostics(
        SearchResult retrieval,
        IReadOnlyList<string> candidateDocumentIds,
        IReadOnlyList<ContextChunk> context,
        string model,
        Stopwatch stopwatch,
        ModelUsage? usage) => new(
        retrieval.Mode.ToApi(),
        retrieval.MaxDenseScore,
        retrieval.MaxLexicalCoverage,
        candidateDocumentIds,
        context.Select(source => new ContextSourceDto(source.Label, source.Chunk.DocumentId, source.Chunk.Version, source.Chunk.SectionPath)).ToList(),
        model,
        stopwatch.ElapsedMilliseconds,
        usage?.InputTokens,
        usage?.OutputTokens,
        usage?.Calls ?? 0);

    /// <summary>
    /// Sunucuda doğrulanmış bir çelişki: modelin seçtiği ve elediği bağlam bölümleri ile bunlardan kurala göre
    /// kaybedenler.
    /// </summary>
    /// <remarks>
    /// API'deki <see cref="ConflictDto"/> doküman kimlikleri taşır; handler'ın ise kuralı zorlamak için bağlam bölümlerine
    /// (hangi bölüm çıkarılacak, hangi atıf kaybedene işaret ediyor) ihtiyacı vardır. Bu kayıt ikisini birleştirir ve API
    /// biçimine yanıtın sonunda çevrilir.
    /// </remarks>
    /// <param name="Topic">Modelin bildirdiği çelişki konusu.</param>
    /// <param name="Chosen">Modelin geçerli kabul ettiği bölüm.</param>
    /// <param name="Rejected">Modelin elediği bölümler (bağlamda olanlar, tekrarsız).</param>
    /// <param name="Reason">Modelin seçim gerekçesi.</param>
    private sealed record CheckedConflict(string Topic, ContextChunk Chosen, IReadOnlyList<ContextChunk> Rejected, string Reason)
    {
        /// <summary>
        /// Sunucunun kuralı uyguladığı durumda yazılan gerekçe. Modelin seçiminin düzeltildiğini ve kararın hangi kurala
        /// dayandığını yanıtın içinde açıkça söyler.
        /// </summary>
        private const string EnforcedReasonPrefix = "Sunucu öncelik kuralını uyguladı (model daha düşük öncelikli kaynağı seçmişti): ";

        /// <summary>Çelişkinin tüm üyeleri: önce seçilen, ardından elenen bölümler.</summary>
        private IReadOnlyList<ContextChunk> Members { get; } = [Chosen, .. Rejected];

        /// <summary>
        /// Kurala göre kaybeden bölümler (<see cref="SourcePrecedence.Losers"/>). Düzeltme turunda bağlamdan çıkarılırlar;
        /// yanıtın kabul edilen bir atfı bunlardan birine işaret ediyorsa yanıt da ihlal sayılır.
        /// </summary>
        public IReadOnlyList<ContextChunk> Losers { get; } = SourcePrecedence.Losers([Chosen, .. Rejected]);

        /// <summary>
        /// Modelin seçimi kurala uyuyorsa true: seçilen bölüm kaybedenler arasında değildir. Eşit yetki ve eşit tarihte
        /// kural kaynakları ayırt edemediğinden modelin seçimi kurala uygun sayılır.
        /// </summary>
        public bool RuleSatisfied => !Losers.Contains(Chosen);

        /// <summary>
        /// Kurala göre geçerli kaynak: model kurala uyduysa seçtiği bölüm, uymadıysa kaybedenler dışındaki ilk üye (eşitlikte
        /// modelin sıralaması korunur). Yanıtın bu kaynağın dokümanına atıf yapması beklenir; yapmıyorsa çelişki beyanı ile
        /// yanıt birbirini tutmaz.
        /// </summary>
        public ContextChunk Winner => Members.First(member => !Losers.Contains(member));

        /// <summary>Çelişkiyi modelin bildirdiği hâliyle, sunucunun <c>RuleSatisfied</c> kararıyla birlikte API biçimine çevirir.</summary>
        public ConflictDto ToDto() =>
            new(Topic, ToConflictSource(Chosen), Rejected.Select(ToConflictSource).ToList(), Reason, RuleSatisfied);

        /// <summary>
        /// Düzeltme turundan önce, sunucunun kararını gösteren çelişki kaydını üretir. Model kurala uyduysa kayıt modelin
        /// kaydıdır; uymadıysa seçilen kaynak kuralın kazananı, elenenler kaybedenler, gerekçe sunucunun kuralı
        /// uyguladığını söyleyen metindir ve <c>RuleSatisfied</c> true olur.
        /// </summary>
        /// <remarks>
        /// Kaybeden bölümler bağlamdan çıkarıldığı için model ikinci denemede bu çelişkiyi bir daha göremez ve bildiremez.
        /// Kayıt saklanmasaydı yanıt, kaynakların çeliştiğini ve güncel olanın nasıl seçildiğini göstermezdi.
        /// </remarks>
        public ConflictDto ToEnforcedDto()
        {
            if (RuleSatisfied)
            {
                return ToDto();
            }

            return new ConflictDto(
                Topic,
                ToConflictSource(Winner),
                Losers.Select(ToConflictSource).ToList(),
                EnforcedReasonPrefix + SourcePrecedence.Rule,
                RuleSatisfied: true);
        }
    }

    /// <summary>
    /// Bir sorudaki gerçek model isteklerinin sayısını ve toplam token kullanımını biriktirir; soru başına çağrı bütçesi
    /// ve tanılamadaki <c>modelCalls</c> buradan gelir.
    /// </summary>
    /// <remarks>
    /// Sayım üreticinin bildirdiği gerçek istek sayısıyla (<see cref="GeneratedAnswer.Attempts"/>) yapılır; şema
    /// düzeltmesi için yapılan yeniden deneme de sayılır. Yalnızca son çağrının token sayısını raporlamak düzeltme turunun
    /// maliyetini gizlerdi. Sağlayıcı kullanım bilgisi döndürmezse toplam null kalır; bilinmeyen bir değer 0 diye
    /// gösterilmez.
    /// </remarks>
    private sealed class ModelUsage
    {
        /// <summary>Şimdiye kadar sunucuya giden model isteği sayısı (üreticinin yeniden denemeleri dahil).</summary>
        public int Calls { get; private set; }

        /// <summary>Toplam girdi token sayısı; hiçbir çağrı bildirmediyse null.</summary>
        public long? InputTokens { get; private set; }

        /// <summary>Toplam çıktı token sayısı; hiçbir çağrı bildirmediyse null.</summary>
        public long? OutputTokens { get; private set; }

        /// <summary>
        /// Bir model yanıtını sayaçlara ekler; yanıtın gerektirdiği istek sayısı kadar bütçe harcanır (en az bir).
        /// </summary>
        public void Add(GeneratedAnswer generated)
        {
            Calls += Math.Max(1, generated.Attempts);
            InputTokens = Sum(InputTokens, generated.InputTokens);
            OutputTokens = Sum(OutputTokens, generated.OutputTokens);
        }

        /// <summary>Bilinen değerleri toplar; yeni değer bilinmiyorsa (null) mevcut toplamı korur.</summary>
        private static long? Sum(long? total, long? value) => value is null ? total : (total ?? 0) + value;
    }
}
