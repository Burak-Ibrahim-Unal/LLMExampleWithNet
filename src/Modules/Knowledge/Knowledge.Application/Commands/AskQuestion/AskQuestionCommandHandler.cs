using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.BusinessRules;
using Knowledge.Application.Contracts;
using Knowledge.Application.Exceptions;
using Knowledge.Application.Options;
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
/// iş kuralları (boş soru, 500 karakter sınırı, indeksin hazır olması) → hibrit arama (BM25 + varsa vektör benzerliği,
/// RRF ile birleştirilir) → Kapı 1 (<see cref="AnswerabilityPolicy"/>: bilgi tabanında soruya yeterince yakın bir şey
/// var mı?) → sürüm çözümleme (<see cref="VersionResolver"/>: her doküman ailesinde yalnızca yürürlükteki sürüm kalır,
/// sadece eski sürüm eşleştiyse güncel sürümün bölümleri onun yerine konur, yürürlükte kaynak kalmazsa
/// <c>NoSourceInEffect</c> reddi) → dil modeli → Kapı 2 (modelin kendi <c>answerable</c> kararı) → Kapı 3
/// (<see cref="CitationValidator"/>: atıflar modele verilen kaynaklara mı dayanıyor?) → kaynaklar arası çelişki
/// kontrolü (<see cref="SourcePrecedence"/>) → yanıt metninin temizlenmesi (<see cref="AnswerText"/>) →
/// <see cref="QuestionLog"/> denetim kaydı.
/// </summary>
/// <remarks>
/// Her kapı yanıtı durdurabilir. Durdurulan yanıt bir hata değil, HTTP 200 ile dönen açık bir "bilmiyorum"dur:
/// <c>answerable=false</c>, sabit Türkçe mesaj, boş <c>sources</c> ve hangi kapıda durulduğunu söyleyen
/// <c>refusalReason</c>. Görevin "dokümanlarda yeterli bilgi yoksa yanıt üretme, bunu açıkça söyle" şartı böyle
/// karşılanır. Ucuz ve deterministik adımlar (iş kuralları, Kapı 1, sürüm çözümleme) bilerek pahalı ve deterministik
/// olmayan LLM çağrısından önce gelir: alakasız ya da yalnızca eski sürümlere dayanan sorularda model hiç çağrılmaz ve
/// model eski bir kuralı hiçbir zaman görmez. Modelden sonraki kontroller ise model çıktısına körü körüne güvenmemek
/// içindir. Böylece kararların çoğu prompt'a bırakılmaz, birim testleriyle doğrulanabilen kodda verilir.
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
    /// kadar geçen süredir (model çağrıldıysa onu da kapsar).
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
        var context = WithSubstitutes(retrieval.Hits, resolution, prepared)
            .Take(settings.TopK)
            .Select((hit, position) => new ContextChunk($"C{position + 1}", hit.Chunk))
            .ToList();

        // İlgili her şey yürürlükten kalkmış ya da henüz yürürlüğe girmemiş: modelin kullanabileceği bir kaynak yok.
        // Eski kuralı modele göstermek yerine model çağrılmadan NoSourceInEffect gerekçesiyle reddedilir.
        if (context.Count == 0)
        {
            return await RefuseAsync(question, RefusalReasons.NoSourceInEffect, string.Empty, Diagnostics(retrieval, candidateDocumentIds, context, string.Empty, stopwatch, null), cancellationToken);
        }

        // Dil modeli çağrısı. Buradaki hatalar "bilgi yok" reddi değil gerçek hatalardır: erişilemeyen sunucu 503, adaptörün
        // tek yeniden denemesinden sonra da şemaya uymayan çıktı 502 olarak döner.
        GeneratedAnswer generated;

        try
        {
            generated = await generator.GenerateAsync(question, context, cancellationToken: cancellationToken);
        }
        catch (AnswerGenerationException exception)
        {
            logger.LogWarning(exception, "Answer generation failed ({Failure}).", exception.Failure);

            return exception.Failure == AnswerGenerationFailure.Unavailable
                ? ApiResult<AnswerDto>.Fail(Messages.Knowledge.LlmUnavailable, 503)
                : ApiResult<AnswerDto>.Fail(Messages.Knowledge.LlmInvalidOutput, 502);
        }

        var diagnostics = Diagnostics(retrieval, candidateDocumentIds, context, generated.Model, stopwatch, generated);

        // Kapı 2: model verilen kaynakları yetersiz bulduysa yanıt uydurmaz; neyin eksik olduğunu belirterek reddeder.
        if (!generated.Answerable)
        {
            return await RefuseAsync(question, RefusalReasons.ModelInsufficientContext, generated.MissingInformation, diagnostics, cancellationToken);
        }

        // Kapı 3: yalnızca modele verilen C1..Cn etiketlerinden birine işaret eden atıflar kalır; alıntının o bölümde
        // gerçekten geçip geçmediği ayrıca quoteVerified olarak işaretlenir. Geçerli atıf yoksa yanıt kaynaksızdır.
        var citations = CitationValidator.Validate(generated.Citations, context);

        if (citations.Count == 0)
        {
            return await RefuseAsync(question, RefusalReasons.NoValidCitations, generated.MissingInformation, diagnostics, cancellationToken);
        }

        // Model talimatlara rağmen metne "[C1]" gibi kaynak işaretleri ya da alıntı kopyaları bırakabilir. Kaynaklar kendi
        // alanında taşındığı için bunlar temizlenir; yanıt müşteriye temiz okunmalıdır.
        var answerText = AnswerText.Clean(generated.Answer, citations.Select(citation => citation.Quote).ToList());

        // Modelin metni yalnızca kaynak işaretlerinden ya da alıntılardan oluşuyorsa temizlikten sonra boş kalır; bu durumda
        // atıf yapılan alıntıların kendisi yanıt olur.
        if (string.IsNullOrWhiteSpace(answerText))
        {
            answerText = string.Join(" ", citations.Select(citation => citation.Quote).Where(quote => quote.Length > 0).Distinct());
        }

        // Alıntılar da boşsa müşteriye gösterilecek bir metin yoktur; boş bir "yanıt" yerine Kapı 3 reddi döner.
        if (string.IsNullOrWhiteSpace(answerText))
        {
            return await RefuseAsync(question, RefusalReasons.NoValidCitations, generated.MissingInformation, diagnostics, cancellationToken);
        }

        // Sürüm kararları yalnızca yanıtın gerçekten dayandığı (atıf yapılan) doküman aileleri için raporlanır; bağlama
        // girip yanıtta kullanılmayan bir ailenin kararı kullanıcıyı yanlış dokümana yönlendirirdi.
        var citedFamilies = citations.Select(citation => citation.Source.Chunk.DocumentKey).ToHashSet(StringComparer.Ordinal);

        var answer = new AnswerDto(
            question,
            Answerable: true,
            Answer: answerText,
            Sources: citations.Select(ToSourceDto).ToList(),
            VersionResolution: ToDto(resolution, citedFamilies),
            Conflicts: CheckConflicts(generated.Conflicts, context),
            MissingInformation: generated.MissingInformation.Trim(),
            RefusalReason: string.Empty,
            Diagnostics: diagnostics);

        await LogAsync(answer, cancellationToken);
        return ApiResult<AnswerDto>.Ok(answer);
    }

    /// <summary>
    /// Açık bir "dokümanlarda yeterli bilgi yok" yanıtı oluşturur, denetim kaydına yazar ve HTTP 200 ile döndürür.
    /// <paramref name="reason"/> hangi kapıda durulduğunu söyleyen <see cref="RefusalReasons"/> değeridir;
    /// <paramref name="missingInformation"/> modelin neyin eksik olduğuna dair açıklamasıdır (model çağrılmadıysa boş).
    /// </summary>
    /// <remarks>
    /// Ret bir hata değil geçerli bir iş sonucudur: istemci <c>answerable=false</c>, boş <c>sources</c> ve yanıt alanında
    /// her zaman aynı sabit Türkçe mesajı alır; modelin ürettiği metin retlerde hiçbir zaman yanıt gibi gösterilmez.
    /// Retler sürüm kararı taşımaz (yalnızca kural metni, boş listeler): sürüm kararları bir yanıtın kaynaklarını
    /// açıklamak içindir, yanıt yoksa açıklanacak kaynak da yoktur. Tanılama ise bilerek doldurulur; neyin bulunduğu ve
    /// modele neyin gösterildiği görülebilsin, "neden reddedildi?" sorusu yanıttan ve denetim kaydından cevaplanabilsin.
    /// </remarks>
    private async Task<ApiResult<AnswerDto>> RefuseAsync(
        string question,
        string reason,
        string missingInformation,
        AnswerDiagnosticsDto diagnostics,
        CancellationToken cancellationToken)
    {
        var refusal = new AnswerDto(
            question,
            Answerable: false,
            Answer: Messages.Knowledge.NotEnoughInformation,
            Sources: [],
            VersionResolution: new VersionResolutionDto(false, VersionResolver.Rule, [], []),
            Conflicts: [],
            MissingInformation: missingInformation.Trim(),
            RefusalReason: reason,
            Diagnostics: diagnostics);

        await LogAsync(refusal, cancellationToken);
        return ApiResult<AnswerDto>.Ok(refusal, Messages.Knowledge.NotEnoughInformation);
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
    /// Modelin bildirdiği, farklı dokümanlar arasındaki çelişkileri sunucu tarafında doğrular ve API biçimine çevirir.
    /// Seçilen kaynak modele verilen bağlamda yoksa ya da geriye geçerli bir reddedilen kaynak kalmazsa çelişki atılır.
    /// Kalan her çelişki için modelin seçiminin öncelik kuralına uyup uymadığı (<see cref="SourcePrecedence"/>:
    /// politika/prosedür &gt; kılavuz &gt; SSS, eşit yetkide daha yeni yürürlük tarihi) <c>RuleSatisfied</c> alanında
    /// raporlanır.
    /// </summary>
    /// <remarks>
    /// Görev, kaynaklar çeliştiğinde güncel olanın nasıl seçildiğinin gösterilmesini istiyor; bunu yalnızca modelin
    /// beyanına bırakmak, modelin yanlış kaynağı seçtiği durumları gizlerdi. Model etiketleri farklı biçimlerde
    /// yazabildiği için ("c2", "[C2]", "2") etiketler önce normalize edilir; bağlamda olmayan etiketler sessizce elenir,
    /// seçilen kaynak reddedilenler arasında sayılmaz, tekrarlar ayıklanır. Kural ihlali yanıtı reddettirmez, yalnızca
    /// görünür kılar; değerlendirme raporu da bu alanı gösterir. Aynı belgenin sürümleri arasındaki seçim burada değil,
    /// model çağrılmadan önce <see cref="VersionResolver"/> tarafından yapılır.
    /// </remarks>
    private static IReadOnlyList<ConflictDto> CheckConflicts(IReadOnlyList<GeneratedConflict> conflicts, IReadOnlyList<ContextChunk> context)
    {
        var sourcesByLabel = context.ToDictionary(source => source.Label, StringComparer.OrdinalIgnoreCase);
        var checkedConflicts = new List<ConflictDto>();

        foreach (var conflict in conflicts)
        {
            if (!sourcesByLabel.TryGetValue(SourceLabel.Normalize(conflict.ChosenChunkLabel), out var chosen))
            {
                continue;
            }

            var rejected = conflict.RejectedChunkLabels
                .Select(SourceLabel.Normalize)
                .Distinct()
                .Where(label => label != chosen.Label && sourcesByLabel.ContainsKey(label))
                .Select(label => sourcesByLabel[label])
                .ToList();

            if (rejected.Count == 0)
            {
                continue;
            }

            checkedConflicts.Add(new ConflictDto(
                conflict.Topic.Trim(),
                ToConflictSource(chosen),
                rejected.Select(ToConflictSource).ToList(),
                conflict.Reason.Trim(),
                RuleSatisfied: rejected.All(other => SourcePrecedence.Outranks(chosen.Chunk, other.Chunk))));
        }

        return checkedConflicts;
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
    /// Doğrulanmış bir atıfı yanıttaki <c>sources</c> öğesine çevirir: doküman kimliği, başlık, sürüm, yürürlük tarihi,
    /// durum, tür, bölüm yolu, alıntı ve <c>quoteVerified</c>. Görevin "her yanıt kullandığı dokümanı ve ilgili bölümü
    /// göstermeli" şartı bu alanlarla karşılanır; <c>quoteVerified</c> da alıntının kaynakta gerçekten geçip geçmediğini,
    /// yani uydurulmuş ya da başka sözcüklerle yazılmış olabileceğini istemciye açıkça gösterir.
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
    /// etiketleriyle verilen bölümler, model adı, gecikme ve varsa token sayıları.
    /// </summary>
    /// <remarks>
    /// Hem yanıtlarda hem retlerde doldurulur; "neden reddedildi, neden bu kaynak?" soruları ancak bu verilerle
    /// cevaplanabilir. Model adı, adaptörün döndürdüğü <c>GeneratedAnswer.Model</c> değeridir, yani yapılandırılmış model
    /// adıdır (llama.cpp'nin bildirdiği kimlik yerel model dosyasının yolunu içerdiği için kullanılmaz); model çağrılmadan
    /// verilen retlerde boştur. <paramref name="generated"/> da bu yüzden null olabilir; o durumda token sayısı yoktur.
    /// Gecikme, kronometrenin başladığı andan (iş kurallarından sonra) bu metodun çağrıldığı ana kadar geçen süredir.
    /// </remarks>
    private static AnswerDiagnosticsDto Diagnostics(
        SearchResult retrieval,
        IReadOnlyList<string> candidateDocumentIds,
        IReadOnlyList<ContextChunk> context,
        string model,
        Stopwatch stopwatch,
        GeneratedAnswer? generated) => new(
        retrieval.Mode.ToApi(),
        retrieval.MaxDenseScore,
        retrieval.MaxLexicalCoverage,
        candidateDocumentIds,
        context.Select(source => new ContextSourceDto(source.Label, source.Chunk.DocumentId, source.Chunk.Version, source.Chunk.SectionPath)).ToList(),
        model,
        stopwatch.ElapsedMilliseconds,
        generated?.InputTokens,
        generated?.OutputTokens);
}
