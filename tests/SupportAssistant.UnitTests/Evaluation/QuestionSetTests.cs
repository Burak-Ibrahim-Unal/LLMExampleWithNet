using System.Text.Json;
using Knowledge.Application.Contracts;
using Shouldly;
using SupportAssistant.Eval;
using static SupportAssistant.UnitTests.Evaluation.EvalAnswers;

namespace SupportAssistant.UnitTests.Evaluation;

/// <summary>
/// Depodaki soru dosyalarının (<c>eval/questions*.json</c>) testleri: dosyaların bilgi tabanıyla tutarlılığı ve gerçek
/// beklentilerin doğru yanıtları geçirip ters, olumsuzlanmış ya da yanlış öncülü kabul eden yanıtları kaldırması.
/// Kontrollerin kendisi <see cref="EvalChecksTests"/>'te sınanır.
/// </summary>
/// <remarks>
/// Bağımsız setlerin ve halüsinasyon setinin satırları beklentiler yazılırken ve ilk koşudan önce eklendi; koşu
/// sonuçlarına göre değiştirilmez.
/// </remarks>
public sealed class QuestionSetTests
{
    /// <summary>
    /// Soru dosyalarındaki gerçek beklentilerin (<c>eval/questions*.json</c>) kritik bir kararın koşulunu tersine çeviren,
    /// olumsuzlayan, eski kuralı söyleyen ya da sorudaki yanlış öncülü kabul eden yanıtları kaldırdığını doğrular. İlk satır
    /// arkadaş incelemesinin örneğidir: doğru kaynak, doğru bölüm, doğrulanmış alıntı ve kaynakta geçen sayıyla "750 TL
    /// altındaki siparişlerde kargo ücretsizdir" diyen yanıt önceki beklentilerin hepsinden geçiyordu.
    /// </summary>
    /// <remarks>
    /// Beklentiler veri dosyasından okunur; dosyadaki bir koşul grubu ya da yasak ifade silinirse bu test kırılır. Her
    /// yanıt beklenen dokümana ve bölüme atıf yapar ve sayıları kaynakta ya da soruda geçer; kalma nedeni yalnızca içerik,
    /// koşul ya da yasak ifade kontrolleridir. Bağımsız setlerin ve halüsinasyon setinin satırları, beklentiler yazılırken
    /// ve ilk koşudan önce eklendi; koşu sonuçlarına göre değiştirilmez. Kontroller ifade tabanlı olduğu için kısmidir:
    /// buradaki örnekler yakalanır, ama her ters anlatım yakalanmaz; değerlendirme raporundaki yanıtlar bu yüzden ayrıca
    /// elle okunur.
    /// </remarks>
    [Theory]
    [InlineData("N04", "750 TL altındaki siparişlerde kargo ücretsizdir.")]
    [InlineData("N04", "Kargo, 750 TL'nin altındaki siparişlerde ücretsizdir.")]
    [InlineData("N04", "Kargo yalnızca 750 TL'den az tutarlı siparişlerde ücretsizdir.")]
    [InlineData("N04", "750 TL ve üzerindeki siparişlerde kargo ücretsiz değildir.")]
    [InlineData("N04", "750 TL ve üzerindeki siparişlerde kargo ücretsiz sayılmaz; kargo ücreti alınır.")]
    [InlineData("C01", "Ürünü teslim aldıktan 30 gün sonra iade edebilirsiniz.")]
    [InlineData("C02", "Ücret, ürün depoya ulaştıktan 5 iş günü sonra hesabınıza geçer.")]
    [InlineData("N08", "Ürün depoya ulaştıktan 5 iş gününden sonra paranız iade edilir.")]
    [InlineData("N03", "Su hasarı garanti kapsamı dışında değildir; cihazınız ücretsiz onarılır.")]
    [InlineData("N03", "Hayır, endişelenmeyin: sıvı teması garanti kapsamındadır.")]
    [InlineData("N10", "Onarım, cihaz servise ulaştıktan 20 iş günü sonra başlar.")]
    [InlineData("N12", "Hasarlı ürünü teslimattan itibaren 3 gün sonra bildirebilirsiniz.")]
    [InlineData("C05", "Çağrı merkezimiz hafta sonları ve resmî tatillerde hizmet vermez; hafta içi ulaşabilirsiniz.")]
    [InlineData("C07", "İade kodunuz talebiniz incelendikten sonra en geç 2 iş günü içinde e-posta ile iletilir.")]
    [InlineData("H23", "Hayır, iade süresi geçtiği için iade talebiniz kabul edilmez.")]
    [InlineData("HL07", "Evet, garanti süresi 3 yıl olduğu için cihazınız hâlâ garanti kapsamındadır.")]
    [InlineData("HL08", "Evet, 500 TL üzeri siparişlerde kargo ücretsiz olduğu için 600 TL'lik siparişinizde kargo ücreti alınmaz.")]
    [InlineData("HL09", "Evet, bir Lumora hesabına 15 cihaz ekleyebilirsiniz.")]
    [InlineData("HL10", "Evet, 14 gün kuralı hâlâ geçerlidir; ürünü 14 gün içinde iade edebilirsiniz.")]
    [InlineData("HL11", "L2'ye aktarılan talepler 24 saat içinde sonuçlandırılır.")]
    [InlineData("HL12", "Evet, seri numarasını uygulamadaki Cihaz Bilgileri ekranından alarak başvurabilirsiniz; arızanız garanti kapsamında onarılır.")]
    public void Reversed_or_negated_conditions_fail_the_real_expectations(string id, string answer)
    {
        var question = RealQuestion(id);

        var checks = EvalChecks.Evaluate(question.Expect, CitingExpected(question, answer), question.Question, KnowledgeBaseTexts.Value);

        checks.Where(check => check.Name is "kaynak" or "bölüm" or "sayılar kaynakta").ShouldAllBe(check => check.Passed);
        checks.Where(check => check.Name is "içerik" or "koşul" or "yasak ifade").ShouldContain(check => !check.Passed);
    }

    /// <summary>
    /// Kayıtlı canlı koşulardaki (varsayılan ve düşünme modu açık) gerçek, doğru yanıtların sıkılaştırılmış beklentilerin
    /// bütün kontrollerinden geçtiğini doğrular.
    /// </summary>
    /// <remarks>
    /// Yeni kontroller yanlış yanıtları kaldırırken doğru yanıtları da kaldırsaydı değerlendirme bu kez ters yönde
    /// yanıltıcı olurdu. Yanıt metinleri <c>eval/results</c> altındaki raporlardan aynen alındı.
    /// </remarks>
    [Theory]
    [InlineData("N03", "Maalesef, sıvı teması, nem veya su hasarı gibi durumlar garanti kapsamı dışında yer almaktadır.")]
    [InlineData("N03", "Hayır, sıvı teması, nem veya su hasarı gibi durumlar garanti kapsamı dışındadır.")]
    [InlineData("N04", "750 TL ve üzerindeki siparişlerde kargo ücretsizdir.")]
    [InlineData("N08", "İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir.")]
    [InlineData("C01", "Ürünü, teslim aldığınız tarihten itibaren 30 gün içinde iade edebilirsiniz. Bu süre, kargo firmasının teslimat kaydındaki tarih esas alınarak hesaplanmaktadır.")]
    [InlineData("C01", "Ürünü teslim aldığınız tarihten itibaren 30 gün içinde iade talebinde bulunabilirsiniz. Ancak, ürün hasarlı veya eksik ise teslimattan itibaren 3 gün içinde bildirimde bulunmanız gerekmektedir.")]
    [InlineData("C02", "İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir.")]
    public void Recorded_correct_answers_pass_the_real_expectations(string id, string answer)
    {
        var question = RealQuestion(id);

        var checks = EvalChecks.Evaluate(question.Expect, CitingExpected(question, answer), question.Question, KnowledgeBaseTexts.Value);

        checks.ShouldAllBe(check => check.Passed);
    }

    /// <summary>
    /// Kayıtlı koşularda görülmemiş ama doğru olan yazımların da gerçek beklentilerden geçtiğini doğrular: "750 TL üstü",
    /// "750 TL veya üzeri", eşiğin altındaki siparişler için doğru bir olumsuzlama ("ücretsiz kargo uygulanmaz"),
    /// sorunun kendi kalıbıyla verilen "5 iş gününde" ve yeni soruların makul doğru yanıtları. Yanlış bir öncülü düzelten
    /// yanıtlar öncülü tekrar eder ("garanti süresi 3 yıl değil, 2 yıldır"); bu tekrar yasak ifadeye takılmamalıdır.
    /// </summary>
    /// <remarks>
    /// Kod incelemesi, ilk koşul ve yasak ifade listelerinin bu doğru yanıtları kaldırdığını gösterdi; kayıtlı koşular
    /// yalnızca model bilgi tabanının ifadesini kopyaladığı için geçiyordu. Liste genişletildi ve ters yazımları yakalayan
    /// yasak ifadeler eşiğe bağlı biçimlere daraltıldı; ters yazımlar yine kalır (yukarıdaki test). Türkçede olumsuzluk
    /// çoğu zaman ayrı bir "değil" sözcüğüyle kurulduğu ve ifade eşleşmesi sözcük sonunu açık bıraktığı için yeni setlerin
    /// yasak ifadeleri olumlu eklerle biter ("…ücretsizdir", "…geçerlidir"); "ücretsiz değildir" bunlarla eşleşmez.
    /// </remarks>
    [Theory]
    [InlineData("N04", "Kargo, 750 TL üstü siparişlerde ücretsizdir.")]
    [InlineData("N04", "750 TL veya üzeri siparişlerde kargo ücretsizdir.")]
    [InlineData("N04", "750 TL ve üzeri siparişlerde kargo ücretsizdir; 750 TL altındaki siparişlerde ücretsiz kargo uygulanmaz.")]
    [InlineData("C02", "İade edilen ürün depoya ulaşıp kontrol edildikten sonra paranız 5 iş gününde hesabınıza geçer.")]
    [InlineData("N08", "Ürün depoya ulaşıp kontrol edildikten sonra ücret 5 iş gününde kartınıza iade edilir.")]
    [InlineData("N09", "Garanti başvurusunu Lumora uygulamasında Destek > Garanti Talebi menüsünden yapabilirsiniz; cihazın seri numarası ve fatura gerekir.")]
    [InlineData("N10", "Garanti kapsamındaki onarım, cihazın servise ulaşmasından itibaren en geç 20 iş günü içinde tamamlanır.")]
    [InlineData("N12", "Hasarlı ürünü teslimattan itibaren 3 gün içinde fotoğraflarla birlikte uygulamadaki Destek > Teslimat Sorunu menüsünden bildirin; ürün ücretsiz olarak değiştirilir.")]
    [InlineData("N14", "Kombiden gelen kabloları montaj plakasındaki COM ve NO uçlarına bağlayın.")]
    [InlineData("C05", "Çağrı merkezimize hafta sonu ve resmî tatiller dahil her gün 08:00–22:00 saatleri arasında ulaşabilirsiniz.")]
    [InlineData("C07", "İade kodu, talebi oluşturduğunuz anda uygulamada görüntülenir.")]
    [InlineData("H14", "Cihaz yazılımı her gece 03:00'te otomatik olarak güncellenir; güncelleme sırasında cihazın elektriğini kesmeyin.")]
    [InlineData("H22", "Evet, çağrı merkezi hafta sonu dahil her gün 08:00–22:00 arasında hizmet verir.")]
    [InlineData("H23", "Evet, ürünü teslim aldığınız tarihten itibaren 30 gün içinde iade edebilirsiniz.")]
    [InlineData("HL07", "Hayır. Garanti süresi 3 yıl değil, fatura tarihinden itibaren 2 yıldır; 2,5 yıl önce aldığınız cihazın garantisi dolmuştur.")]
    [InlineData("HL08", "Hayır, ödersiniz: kargo 750 TL ve üzerindeki siparişlerde ücretsizdir; 600 TL'lik siparişiniz için 49,90 TL kargo ücreti alınır.")]
    [InlineData("HL09", "Hayır, bir Lumora hesabına en fazla 10 cihaz eklenebilir; 15 cihaz eklenemez.")]
    [InlineData("HL10", "Hayır, 14 günlük süre artık geçerli değil; güncel politikaya göre ürünü teslim aldığınız tarihten itibaren 30 gün içinde iade edebilirsiniz.")]
    [InlineData("HL11", "L2 talepleri en geç 2 iş günü içinde sonuçlandırılır; L2 ayrıca 24 saat içinde müşteriye dönüş yapar.")]
    [InlineData("HL12", "Hayır. Seri numarası etiketi okunamaz hâle gelmiş cihazlar garanti kapsamı dışındadır.")]
    public void Plausible_correct_phrasings_pass_the_real_expectations(string id, string answer)
    {
        var question = RealQuestion(id);

        var checks = EvalChecks.Evaluate(question.Expect, CitingExpected(question, answer), question.Question, KnowledgeBaseTexts.Value);

        checks.ShouldAllBe(check => check.Passed);
    }

    /// <summary>
    /// Depodaki bütün soru dosyalarının (ana set, bağımsız setler, halüsinasyon seti) bilgi tabanıyla tutarlı olduğunu
    /// doğrular: en az dört set var; kimlikler bütün setlerde benzersiz; kategoriler raporun tanıdığı üç anahtardan biri;
    /// anılan doküman kimlikleri bilgi tabanında var; beklenen her bölüm adı beklenen kaynaklardan birinin bir başlığında
    /// geçiyor; cevapsız sorular hiçbir içerik beklentisi taşımıyor.
    /// </summary>
    /// <remarks>
    /// Bu hataların hiçbiri değerlendirmede kendini göstermezdi: yanlış yazılmış bir kategori özet tablosundan sessizce
    /// düşer, var olmayan bir doküman kimliği ya da bölüm adı soruyu her koşuda kaldırır, cevapsız bir sorudaki içerik
    /// beklentisi hiç değerlendirilmez. Dosyalar klasörden okunduğu için yeni bir set eklendiğinde test onu da kendiliğinden
    /// kapsar. Bağımsız setler ve halüsinasyon seti bu testten ilk koşudan önce geçer; koşu sonuçlarına göre değiştirilmez.
    /// </remarks>
    [Fact]
    public void Every_question_file_is_consistent_with_the_knowledge_base()
    {
        var questions = QuestionFiles.Value.SelectMany(file => Load(file).Questions.Select(question => (File: Path.GetFileName(file), Question: question))).ToList();

        QuestionFiles.Value.Length.ShouldBeGreaterThanOrEqualTo(4);
        questions.Select(entry => entry.Question.Id).ShouldBeUnique();

        foreach (var (file, question) in questions)
        {
            var label = $"{file} {question.Id}";
            var expect = question.Expect;

            new[] { "normal", "cevapsiz", "celiskili" }.ShouldContain(question.Category, label);

            if (!expect.Answerable)
            {
                expect.ShouldBe(new EvalExpectation(false), label);
                continue;
            }

            var sources = (expect.SourcesAnyOf ?? []).Concat(expect.SourcesAllOf ?? []).ToList();
            IEnumerable<string> conflict = expect.ExpectConflict is { } expected ? [expected.Chosen, .. expected.Rejected] : [];

            sources.ShouldNotBeEmpty(label);
            sources.Concat(expect.ForbiddenSources ?? []).Concat(expect.DiscardedVersions ?? []).Concat(conflict)
                .ShouldAllBe(id => KnowledgeBaseTexts.Value.ContainsKey(id), label);

            foreach (var section in expect.SectionsAnyOf ?? [])
            {
                sources.Any(id => Headings(id).Any(heading => EvalChecks.ContainsPhrase(heading, section))).ShouldBeTrue($"{label}: '{section}'");
            }
        }
    }

    /// <summary>
    /// Depodaki soru dosyaları (<c>eval/questions*.json</c>); depo kökü çalışma klasöründen yukarı doğru, değerlendirme
    /// aracının kullandığı yolla bulunur.
    /// </summary>
    private static readonly Lazy<string[]> QuestionFiles = new(() =>
        Directory.GetFiles(EvalOptions.ResolveFromRepository("eval"), "questions*.json"));

    /// <summary>
    /// Bütün soru dosyalarındaki sorular, kimliğe göre. Kimlikler setler arasında benzersizdir
    /// (<see cref="Every_question_file_is_consistent_with_the_knowledge_base"/>); bir test hangi setteki soruyu kullandığını
    /// yalnızca kimlikle söyler.
    /// </summary>
    private static readonly Lazy<IReadOnlyDictionary<string, EvalQuestion>> RealQuestions = new(() =>
        QuestionFiles.Value.SelectMany(file => Load(file).Questions).ToDictionary(question => question.Id, StringComparer.Ordinal));

    /// <summary>
    /// Gerçek bilgi tabanının doküman metinleri (dosya adı = doküman kimliği). Canlı koşuda bu metinler API'nin doküman
    /// uçlarından alınır; burada aynı dosyalar doğrudan okunur.
    /// </summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> KnowledgeBaseTexts = new(() =>
        Directory.GetFiles(EvalOptions.ResolveFromRepository("knowledge-base"), "*.md")
            .ToDictionary(file => Path.GetFileNameWithoutExtension(file), file => File.ReadAllText(file)));

    /// <summary>Bir soru dosyasını değerlendirme aracının kullandığı JSON ayarlarıyla okur.</summary>
    private static EvalSuite Load(string path) =>
        JsonSerializer.Deserialize<EvalSuite>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    /// <summary>Bir bilgi tabanı dokümanının Markdown başlık satırları (<c>#</c> ile başlayanlar); bölüm adları bunlardır.</summary>
    private static IEnumerable<string> Headings(string documentId) =>
        KnowledgeBaseTexts.Value[documentId].Split('\n').Where(line => line.TrimStart().StartsWith('#'));

    /// <summary>Bütün soru dosyalarından kimliği verilen soruyu döndürür.</summary>
    private static EvalQuestion RealQuestion(string id) => RealQuestions.Value[id];

    /// <summary>
    /// Verilen metinle, sorunun beklediği ilk dokümana ve ilk bölüme atıf yapan ve beklenen eski sürümleri elenmiş olarak
    /// raporlayan bir yanıt kurar; böylece yapısal kontroller geçer ve sonuç yalnızca metnin içeriğine bağlı kalır.
    /// </summary>
    private static AnswerDto CitingExpected(EvalQuestion question, string text) =>
        Answer(
            text,
            sources: [(question.Expect.SourcesAnyOf ?? question.Expect.SourcesAllOf)![0]],
            discarded: question.Expect.DiscardedVersions?.ToArray(),
            section: question.Expect.SectionsAnyOf![0]);
}
