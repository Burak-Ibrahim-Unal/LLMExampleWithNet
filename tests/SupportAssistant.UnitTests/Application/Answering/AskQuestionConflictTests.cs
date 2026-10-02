using Knowledge.Application.Abstractions;
using Knowledge.Application.Answering;
using Knowledge.Application.Commands.AskQuestion;
using Shouldly;
using SupportAssistant.UnitTests.TestDoubles;

namespace SupportAssistant.UnitTests.Application.Answering;

/// <summary>
/// <see cref="AskQuestionCommandHandler"/>'ın kaynaklar arası çelişki testleri: sunucu, modelin bildirdiği çelişkide
/// öncelik kuralını (politika ve prosedür &gt; kılavuz &gt; SSS, eşitlikte yeni tarih) denetler, ihlalde kaybeden
/// bölümleri bağlamdan çıkarıp modeli bir kez daha çağırır ve ihlal sürerse yanıtı reddeder. Kurulum
/// <see cref="AskQuestionHandlerTestBase"/>'tedir.
/// </summary>
public sealed class AskQuestionConflictTests : AskQuestionHandlerTestBase
{
    /// <summary>
    /// Modelin raporladığı dokümanlar arası çelişkinin sunucu tarafında öncelik kuralıyla (<c>SourcePrecedence</c>)
    /// denetlendiğini doğrular. Senaryo gerçek bir çelişkidir: güncel iade politikası iade kargosunun ücretsiz olduğunu,
    /// eski tarihli SSS ise ücretin müşteriye ait olduğunu söyler. Model politikayı seçip SSS'yi elediğinde seçim kurala
    /// uyar: yanıt tek model çağrısıyla kabul edilir ve çelişki, modelin gerekçesiyle ve <c>RuleSatisfied=true</c> olarak
    /// raporlanır.
    /// </summary>
    /// <remarks>
    /// Kurala uyan bir seçimde sunucu araya girmez; çelişki yalnızca görünür kılınır ("kaynaklar çeliştiğinde güncel
    /// olanın nasıl seçildiğini göster" şartı). Bu test kırılırsa doğru seçimler de gereksiz bir düzeltme turuna girer ya
    /// da çelişki kaydı yanıttan kaybolur.
    /// </remarks>
    [Fact]
    public async Task A_conflict_resolved_by_the_precedence_rule_is_reported_as_satisfied()
    {
        _generator.Respond = (_, context) => AnswerWithConflict(context, "iade-v2", ReturnShippingSection, "sss", FaqSection);

        var result = await AskAsync("İade kargo ücretini kim öder?");

        _generator.Calls.ShouldBe(1);
        var conflict = result.Data!.Conflicts.ShouldHaveSingleItem();
        conflict.Chosen.DocumentId.ShouldBe("iade-v2");
        conflict.Rejected.ShouldHaveSingleItem().DocumentId.ShouldBe("sss");
        conflict.Reason.ShouldBe("Modelin gerekçesi.");
        conflict.RuleSatisfied.ShouldBeTrue();
    }

    /// <summary>
    /// Model eski tarihli SSS'yi güncel politikaya tercih ettiğini bildirirse yanıtın kullanıcıya ulaşmadığını, kurala
    /// göre kaybeden SSS bölümü bağlamdan çıkarılarak modelin bir kez daha çağrıldığını doğrular. İkinci yanıt politikaya
    /// dayanır; çelişki kaydı sunucunun kararını gösterir (seçilen politika, elenen SSS, <c>RuleSatisfied=true</c> ve
    /// sunucunun kuralı uyguladığını söyleyen gerekçe). Alıntı sorunu olmadığı için ikinci çağrıda geri bildirim yoktur.
    /// </summary>
    /// <remarks>
    /// Arkadaş incelemesinin kabul ölçütü: model güncel politika yerine eski SSS'yi seçtiğini bildirirse bu çıktı başarılı
    /// cevap olarak kullanıcıya ulaşmaz. Kaybedeni çıkarmak, aynı doküman ailesinin sürümlerinde <c>VersionResolver</c>'ın
    /// yaptığının farklı aileler için karşılığıdır: kararı modele bırakmak yerine kural kodla uygulanır.
    /// </remarks>
    [Fact]
    public async Task When_the_model_prefers_a_lower_ranked_source_the_answer_is_regenerated_without_it()
    {
        _generator.Respond = (_, context) => _generator.Calls == 1
            ? AnswerWithConflict(context, "sss", FaqSection, "iade-v2", ReturnShippingSection)
            : AnswerFrom(context, "iade-v2", ReturnShippingSection);

        var result = await AskAsync("İade kargo ücretini kim öder?");

        _generator.Calls.ShouldBe(2);
        _generator.Feedbacks[1].ShouldBeNull();
        _generator.LastContext.ShouldAllBe(source => source.Chunk.DocumentId != "sss");
        result.Data!.Answerable.ShouldBeTrue();
        result.Data.Sources.ShouldHaveSingleItem().DocumentId.ShouldBe("iade-v2");
        var conflict = result.Data.Conflicts.ShouldHaveSingleItem();
        conflict.Chosen.DocumentId.ShouldBe("iade-v2");
        conflict.Rejected.ShouldHaveSingleItem().DocumentId.ShouldBe("sss");
        conflict.RuleSatisfied.ShouldBeTrue();
        conflict.Reason.ShouldStartWith("Sunucu öncelik kuralını uyguladı");
    }

    /// <summary>
    /// Model çelişkide doğru kaynağı seçtiğini bildirdiği hâlde yanıtında kaybeden SSS bölümüne de atıf yaparsa yanıtın
    /// yeniden üretildiğini doğrular: SSS bağlamdan çıkarılır ve ikinci yanıtın kaynakları yalnızca politikadır.
    /// </summary>
    /// <remarks>
    /// Doğru seçimi beyan etmek yetmez; yanıt elenen kaynağın bilgisine dayanıyorsa eski bilgi yine müşteriye ulaşır.
    /// Bu yüzden ihlal, modelin beyanı kadar yanıtın atıflarına da bakılarak belirlenir.
    /// </remarks>
    [Fact]
    public async Task An_answer_citing_the_losing_source_of_a_conflict_is_regenerated_without_it()
    {
        _generator.Respond = (_, context) =>
        {
            if (_generator.Calls > 1)
            {
                return AnswerFrom(context, "iade-v2", ReturnShippingSection);
            }

            var answer = AnswerWithConflict(context, "iade-v2", ReturnShippingSection, "sss", FaqSection);
            var faq = Cite(context, "sss", FaqSection, "İade kargo ücreti müşteriye aittir.");
            return answer with { Citations = [.. answer.Citations, faq] };
        };

        var result = await AskAsync("İade kargo ücretini kim öder?");

        _generator.Calls.ShouldBe(2);
        _generator.LastContext.ShouldAllBe(source => source.Chunk.DocumentId != "sss");
        result.Data!.Sources.ShouldHaveSingleItem().DocumentId.ShouldBe("iade-v2");
        result.Data.Conflicts.ShouldHaveSingleItem().RuleSatisfied.ShouldBeTrue();
    }

    /// <summary>
    /// Model bir çelişkide doğru kaynağı (politika) seçtiğini bildirdiği hâlde yanıtını yalnızca başka bir dokümana
    /// (kargo politikası) dayandırırsa bunun da ihlal sayıldığını doğrular: SSS bağlamdan çıkarılır, model bir kez daha
    /// çağrılır ve ikinci yanıt politikaya dayanır; çelişki kaydı politikanın SSS'ye üstün geldiğini gösterir.
    /// </summary>
    /// <remarks>
    /// Arkadaş incelemesinin ikinci geçişte doğruladığı açık: çelişki kaydı "politikayı seçtim" derken yanıt başka bir
    /// kaynağa dayanabiliyor ve yine kabul ediliyordu. Seçilen kaynağa atıf yapılmaması, beyan ile yanıtın birbirini
    /// tutmadığını gösterir; kullanıcı da yanıtın neye dayandığı konusunda yanıltılırdı.
    /// </remarks>
    [Fact]
    public async Task A_conflict_whose_winner_is_not_cited_triggers_a_correction_round()
    {
        _generator.Respond = (_, context) =>
        {
            if (_generator.Calls > 1)
            {
                return AnswerFrom(context, "iade-v2", ReturnShippingSection);
            }

            var answer = AnswerWithConflict(context, "iade-v2", ReturnShippingSection, "sss", FaqSection);
            var shipping = context.Single(source => source.Chunk.DocumentId == "kargo");
            return answer with { Citations = [new GeneratedCitation(shipping.Label, shipping.Chunk.Content)] };
        };

        var result = await AskAsync("İade kargo ücretini kim öder?");

        _generator.Calls.ShouldBe(2);
        _generator.LastContext.ShouldAllBe(source => source.Chunk.DocumentId != "sss");
        result.Data!.Sources.ShouldHaveSingleItem().DocumentId.ShouldBe("iade-v2");
        var conflict = result.Data.Conflicts.ShouldHaveSingleItem();
        conflict.Chosen.DocumentId.ShouldBe("iade-v2");
        conflict.RuleSatisfied.ShouldBeTrue();
    }

    /// <summary>
    /// Eşit öncelikli kaynaklar arasındaki bir çelişkide (aynı dokümanın iki bölümü: aynı tür, aynı tarih) model seçtiği
    /// kaynağa atıf yapmazsa, düzeltme turunun bunu söyleyen bir geri bildirimle yapıldığını ve ikinci yanıtın kabul
    /// edildiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Kod incelemesinde bulundu: eşitlikte kurala göre kaybeden olmadığı için bağlamdan hiçbir bölüm çıkarılmaz. Geri
    /// bildirim de verilmeseydi ikinci istek ilkinin birebir aynısı olurdu; sıcaklık 0 ve sabit seed altında aynı yanıt
    /// gelir, ikinci çağrı boşa gider ve yanıt <c>UnresolvedConflict</c> ile reddedilirdi.
    /// </remarks>
    [Fact]
    public async Task A_tied_conflict_without_a_cited_winner_gets_feedback_in_the_correction_round()
    {
        _generator.Respond = (_, context) =>
        {
            if (_generator.Calls > 1)
            {
                return AnswerFrom(context, "iade-v2", ReturnShippingSection);
            }

            var answer = AnswerWithConflict(context, "iade-v2", ReturnShippingSection, "iade-v2", "2. İade Süresi");
            var shipping = context.Single(source => source.Chunk.DocumentId == "kargo");
            return answer with { Citations = [new GeneratedCitation(shipping.Label, shipping.Chunk.Content)] };
        };

        var result = await AskAsync("İade kargo ücretini kim öder?");

        _generator.Calls.ShouldBe(2);
        _generator.Feedbacks[1].ShouldNotBeNull().WinnerNotCited.ShouldBeTrue();
        result.Data!.Answerable.ShouldBeTrue();
        result.Data.Sources.ShouldHaveSingleItem().DocumentId.ShouldBe("iade-v2");
    }

    /// <summary>
    /// Çelişki kayıtlarının yalnızca yanıtın dayandığı dokümanlar için gösterildiğini doğrular: ilk yanıt politika ile SSS
    /// arasındaki çelişkiyi bildirip kargo dokümanına dayanır; düzeltme turundaki yanıt da yalnızca kargo dokümanına dayanır
    /// ve kabul edilir. Politikaya hiç atıf olmadığı için çelişki kaydı yanıtta yer almaz.
    /// </summary>
    /// <remarks>
    /// Sürüm kararlarında olduğu gibi, yanıtın kullanmadığı bir kaynağın çelişki kaydı yanıtı açıklamaz; kullanıcıya
    /// yanıtın politikaya dayandığı izlenimini verirdi.
    /// </remarks>
    [Fact]
    public async Task Conflict_records_are_reported_only_for_documents_the_answer_relies_on()
    {
        _generator.Respond = (_, context) =>
        {
            var shipping = context.Single(source => source.Chunk.DocumentId == "kargo");
            var citation = new GeneratedCitation(shipping.Label, shipping.Chunk.Content);

            if (_generator.Calls > 1)
            {
                return FakeAnswerGenerator.Answer(shipping.Chunk.Content, citation);
            }

            return AnswerWithConflict(context, "iade-v2", ReturnShippingSection, "sss", FaqSection) with { Citations = [citation] };
        };

        var result = await AskAsync("İade kargo ücretini kim öder?");

        _generator.Calls.ShouldBe(2);
        result.Data!.Answerable.ShouldBeTrue();
        result.Data.Sources.ShouldHaveSingleItem().DocumentId.ShouldBe("kargo");
        result.Data.Conflicts.ShouldBeEmpty();
    }

    /// <summary>
    /// Modelin, verilen kaynaklarda olmayan bir kimlikle (<c>C9</c>) çelişki bildirdiği yanıtın sessizce kabul
    /// edilmediğini doğrular: model bir kez daha çağrılır, geri bildirim çelişki kimliklerinin geçersiz olduğunu söyler
    /// (atıflar sorunsuz olduğu için alıntı düzeltmesi istenmez), temiz ikinci yanıt kabul edilir.
    /// </summary>
    /// <remarks>
    /// Geçersiz kimlikli bir çelişki kaydı denetlenemez: kuralın kazananı ve kaybedeni belirlenemediği için öncelik
    /// kuralı uygulanamaz. Böyle bir kaydı yutmak, modelin bildirdiği bir çelişkiyi hiç bildirmemiş gibi saymak olurdu.
    /// </remarks>
    [Fact]
    public async Task A_conflict_report_with_unknown_labels_gets_a_correction_round()
    {
        _generator.Respond = (_, context) =>
        {
            var answer = AnswerFrom(context, "iade-v2", ReturnShippingSection);
            return _generator.Calls == 1
                ? answer with { Conflicts = [new GeneratedConflict("İade kargo ücreti", "C9", [answer.Citations[0].ChunkLabel], "Uydurma kimlik.")] }
                : answer;
        };

        var result = await AskAsync("İade kargo ücretini kim öder?");

        _generator.Calls.ShouldBe(2);
        var feedback = _generator.Feedbacks[1].ShouldNotBeNull();
        feedback.InvalidConflictReferences.ShouldBeTrue();
        feedback.CitationsRejected.ShouldBeFalse();
        result.Data!.Answerable.ShouldBeTrue();
        result.Data.Conflicts.ShouldBeEmpty();
    }

    /// <summary>
    /// Düzeltme turundan sonra da geçersiz kimlikli çelişki bildiren bir yanıtın <c>UnresolvedConflict</c> ile
    /// reddedildiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Model iki denemede de denetlenemeyen bir çelişki beyan ediyorsa, yanıtın hangi kaynağa göre doğru olduğu
    /// söylenemez; çelişkili olabilecek bir yanıtı göstermek yerine açıkça reddedilir.
    /// </remarks>
    [Fact]
    public async Task Invalid_conflict_references_that_persist_are_refused()
    {
        _generator.Respond = (_, context) =>
        {
            var answer = AnswerFrom(context, "iade-v2", ReturnShippingSection);
            return answer with { Conflicts = [new GeneratedConflict("İade kargo ücreti", "C9", ["C10"], "Uydurma kimlikler.")] };
        };

        var result = await AskAsync("İade kargo ücretini kim öder?");

        _generator.Calls.ShouldBe(2);
        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.UnresolvedConflict);
    }

    /// <summary>
    /// Düzeltme turundan sonra da öncelik kuralı ihlal edilirse (ikinci yanıt bu kez daha eski tarihli kargo politikasını
    /// güncel iade politikasına tercih eder) yanıtın <c>UnresolvedConflict</c> gerekçesiyle reddedildiğini ve modelin en
    /// fazla iki kez çağrıldığını doğrular.
    /// </summary>
    /// <remarks>
    /// Model kuralı ikinci kez çiğnediğinde çelişkili bir yanıtı göstermek yerine açıkça reddetmek, görevin "yetersiz ya da
    /// güvenilmez bilgide yanıt üretme" ilkesiyle uyumludur. Deneme sınırı gecikmeyi öngörülebilir tutar.
    /// </remarks>
    [Fact]
    public async Task A_conflict_still_violated_after_the_correction_round_is_refused()
    {
        _generator.Respond = (_, context) => _generator.Calls == 1
            ? AnswerWithConflict(context, "sss", FaqSection, "iade-v2", ReturnShippingSection)
            : AnswerWithConflict(context, "kargo", "2. Kargo Ücreti", "iade-v2", ReturnShippingSection);

        var result = await AskAsync("İade kargo ücretini kim öder?");

        _generator.Calls.ShouldBe(2);
        result.Data!.Answerable.ShouldBeFalse();
        result.Data.RefusalReason.ShouldBe(RefusalReasons.UnresolvedConflict);
        result.Data.Sources.ShouldBeEmpty();
    }

    /// <summary>
    /// İlk yanıt hem doğrulanamayan bir alıntı verir hem de kurala aykırı bir çelişki seçimi bildirir. Tek düzeltme turunun
    /// ikisini birlikte ele aldığını doğrular: ikinci çağrı hem alıntı geri bildirimini alır hem de kaybeden SSS bölümü
    /// olmadan yapılır; toplamda iki çağrı yapılır ve yanıt kabul edilir.
    /// </summary>
    /// <remarks>
    /// Soru başına model çağrısı ikiyle sınırlıdır; iki ayrı sorun için iki ayrı düzeltme turu gecikmeyi üçe katlardı.
    /// </remarks>
    [Fact]
    public async Task Quote_and_precedence_corrections_share_one_correction_round()
    {
        _generator.Respond = (_, context) =>
        {
            if (_generator.Calls > 1)
            {
                return AnswerFrom(context, "iade-v2", ReturnShippingSection);
            }

            var answer = AnswerWithConflict(context, "sss", FaqSection, "iade-v2", ReturnShippingSection);
            return answer with { Citations = [new GeneratedCitation(answer.Citations[0].ChunkLabel, "İade kargosu 900 TL'dir.")] };
        };

        var result = await AskAsync("İade kargo ücretini kim öder?");

        _generator.Calls.ShouldBe(2);
        _generator.Feedbacks[1].ShouldNotBeNull().UnverifiedQuotes.ShouldBe(["İade kargosu 900 TL'dir."]);
        _generator.LastContext.ShouldAllBe(source => source.Chunk.DocumentId != "sss");
        result.Data!.Answerable.ShouldBeTrue();
        result.Data.Sources.ShouldHaveSingleItem().DocumentId.ShouldBe("iade-v2");
    }
}
