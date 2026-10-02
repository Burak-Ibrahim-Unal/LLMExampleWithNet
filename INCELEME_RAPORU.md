# Teknik inceleme raporu — SupportAssistant (düzeltme sonrası)

İnceleme: 2 Ekim 2026, üçüncü geçiş. Kapsam: önceki rapordaki altı bulgunun düzeltmesini taşıyan sekiz commit
(`d63fbcc..0f8ab1d`), tüm ilgili kaynak kod, büyütülen test paketi ve yenilenen değerlendirme çıktıları. Kaynak
kodda değişiklik yapılmadı.

**Sonuç: Düzeltmeler yerinde ve doğru uygulanmış. Önceki rapordaki altı bulgunun beşi tamamen, biri (değerlendirme
kontrolleri) büyük ölçüde kapanmış durumda; ayrıca planda olmayan üç somut iyileştirme daha yapılmış.** Derleme ve
testler bu incelemede yeniden çalıştırıldı. Kalan üç orta/ağırlıktaki bulgu, bir önceki bağımsız inceleme
(`YENIDEN_INCELEME.md`) ile bu incelemenin ayrı ayrı ve aynı sonuca ulaştığı maddelerdir; teslimi engelleyecek
nitelikte değiller, ama kabul ölçütleri açısından açık kalan tek kalemler onlar.

## Doğrulanan sonuçlar

| Kontrol | Sonuç |
|---|---|
| `dotnet build SupportAssistant.slnx` | Başarılı; 0 hata, 0 uyarı |
| `dotnet test --solution SupportAssistant.slnx` | **183 başarılı, 0 başarısız, 0 atlanan** (önceki geçişte 145'ti) |
| Kalibrasyon seti (depodaki rapor, 07:47) | 16/16 · düşünme modu açık koşusu da 16/16 |
| Bağımsız hold-out set (depodaki rapor, 07:48) | 10/12 · kalan iki soru H03 ve H05, ayrıca değerlendirildi |
| Düzeltme turu | Koşularda hiç gerekmemiş (`diagnostics.modelCalls: 1`) |
| Çalışma ağacı | Temiz; `INCELEME_RAPORU.md`, `YAPILACAKLAR.md`, `YENIDEN_INCELEME.md` izsiz |

**Doğrulama sınırı:** Gerçek LLM/embedding sunucularıyla değerlendirme bu incelemede yeniden koşulmadı; 16/16 ve
10/12 sonuçları depoya kayıtlı raporlardır. Kaynak kod, sözleşme ve README iddialarıyla tutarlılığı kod okuyarak
doğrulandı.

## Altı bulgunun durumu

### 1. Doğrulanmamış alıntı — ✅ düzeltildi

Yanıtın kaynakları artık yalnızca alıntısı bölümde birebir doğrulanan atıflardan oluşur
(`AskQuestionCommandHandler.cs:186-187`): `CitationValidator` etiketi bağlamda olmayan atıfları düşürür, handler
içinden yalnızca `QuoteVerified=true` olanlar geçer. Doğrulanamayan atıf kaynak listesine hiç girmiyor. Cevap
metni temizlendikten sonra boşalırsa yanıt **kabul edilmiş** alıntılardan kuruluyor (`:241`) — önceki sürümdeki
"doğrulanmamış alıntı da yanıt olabilir" yolu kapandı. Hiç doğrulanmış atıf yoksa model, doğrulanamayan alıntılar
geri bildirim olarak gösterilerek bir kez daha çağrılıyor; yine olmazsa `NoValidCitations` reddi. README'deki
"doğrulanmış alıntı" iddiası artık davranışla birebir aynı.

### 2. Kaynak önceliği ihlali — ✅ ana akışta düzeltildi

Öncelik ihlali artık iki yoldan yakalanıyor (`AskQuestionCommandHandler.cs:193-194`): model kurala göre kaybeden
kaynağı *seçtiyse* ya da kabul edilmiş atıflardan biri *kaybeden bir bölüme* işaret ediyorsa ihlal sayılıyor.
Düzeltme turunda kaybeden bölümler bağlamdan çıkarılıp model yeniden çağrılıyor; sunucunun kararı
`ToEnforcedDto` ile çelişki kaydı olarak saklanıyor (`:551-565`). İhlal ikinci çağrıdan sonra da sürerse yanıt
yeni `UnresolvedConflict` ret nedeniyle reddediliyor. Başarılı bir yanıttaki her çelişki kaydında `ruleSatisfied`
artık yapısalc olarak true. "Model bildirmezse sunucu göremez" sınırı README'de açıkça yazıyor. Kalan açık aşağıda,
"Kalan bulgular 2"de.

### 3. Değerlendirme kontrolleri — 🟡 büyük ölçüde güçlendirildi

`EvalChecks` artık şunları da denetliyor: ret sözleşmesi (sabit mesaj + boş kaynaklar + dolu `refusalReason`),
beklenen bölüm, bütün kaynakların alıntısının doğrulanmış olması, **yanıttaki her sayının atıf yapılan dokümanda
veya soruda geçmesi**, beklenen çelişki kaydı (seçilen + elenen + `ruleSatisfied=true`). Eski N04 örneği ("999 TL
alınır") artık yakalanıyor. Değerlendiricinin kendisi de testlendi; araca yanıt + soru + doküman metinleri
geçiriliyor ve HTTP 200 + `success` + veri şartı kondu. **Kalan açık:** koşulun *yönü* hâlâ kontrol edilemiyor —
"750 TL altındaki siparişlerde kargo ücretsizdir" ters kararını veren bir yanıt N04'ün mevcut kontrollerinden
geçmeye devam eder. Bu, kod içi açıklamalarda ("ifade kontrolleri anlamsal doğruluğun kanıtı değildir, yanıtlar
elle okunur") bilinen sınır olarak kabul edilmiş durumda; aşağıda "Kalan bulgular 3".

### 4. Eksik JSON'un ret sayılması — ✅ düzeltildi

`AnswerPayload`'ın tüm özellikleri artık C# `required` işaretli (şemada zorunlu alan + ayrıştırmada eksik alan
hatası) ve `IsComplete` (`OpenAiCompatibleAnswerGenerator.cs:163-166`) liste öğelerinin null olmasını reddediyor.
`{}`, null liste öğesi ya da ayrıştırılamayan çıktı düzeltici mesajla bir kez denenip olmazsa `502` döner;
`answerable=true` + boş metin de geçersiz sayılıyor. Zaman aşımı açıkça `503`'e bağlandı. Bozuk çıktının
`ModelInsufficientContext` olarak geçme yolu kapandı; regresyon testleri var.

### 5. Alıntı parçalarının sırası — ✅ düzeltildi

`CitationValidator.IsVerbatim` parçaları artık kaynakta **sırayla ve sözcük başında** arıyor (`:84-147`); rakamla
biten parçanın ardından rakam gelmesi eşleşmeyi geçersiz kılar ("30", "300" içinde bulunmaz; "30 gündür" gibi
Türkçe ekler serbest). Eski `30...iade` örneği artık doğrulanmıyor; ters sıra ve kısaltma için testler var.

### 6. Eval çıkış kodu — ✅ düzeltildi

Çıkış kodları tanımlı ve uygulanıyor: 0 = hepsi geçti, 1 = en az bir soru kaldı, 2 = API'ye ulaşılamadı,
3 = geçersiz argüman (`tools/SupportAssistant.Eval/Program.cs:120`). Bir isteğin çökmesi koşuyu düşürmüyor ama o
soru "geçti" sayılmıyor; arama isabeti ölçülemiyorsa ıska değil "—" olarak raporlanıyor. Araç CI adımı olarak
kullanılabilir.

## Plandaki olmayan (iyi) sürprizler

- **Embedding zaman aşımında BM25'e dönüş** (`56b84f3`): Yanıt vermeyen embedding sunucusunun
  `OperationCanceledException`'ı, çağıranın kendi iptalinden ayrıştırılıp BM25 moduna düşürüyor
  (`KnowledgeIndex.cs:149-158`) — kesinti soruları düşürmüyor.
- **Bağımsız hold-out set** (`d61f774`): Ayar için hiç kullanılmamış 12 soruluk ikinci set; kalibrasyon seti ile
  ayrı raporlanıyor.
- **Düzeltme turu geri bildirimi** (`d63fbcc`): Model, atıflarının neden kabul edilmediğini kendi çıktısı üzerinden
  görüyor — düzeltme turunun şans denemesi değil, öğretici bir adım olması.
- Test paketi 145 → 183'e çıktı; README ve `agent.md` yeni sözleşmelerle hizalandı (health "ok" = yapılandırılmış,
  erişilebilir değil; "doğrulanmış alıntı tüm iddiaları kanıtlamaz" notu dahil).

## Kalan bulgular

Önceki bağımsız inceleme (`YENIDEN_INCELEME.md`) ile bu inceleme, aşağıdaki üç maddeyi birbirinden habersiz
doğruladı; ikisi de aynı kod yollarını işaret ediyor.

### 1. Orta: çağrı bütçesi iki katman birleşince dörde çıkabiliyor ve README iddiasıyla çelişiyor

`MaxModelCalls = 2` (handler) × `MaxAttempts = 2` (adaptörün şema yeniden denemesi) birleşince soru başına en fazla
**4 gerçek sohbet çağrısı** oluşabilir. README (satır 339) "soru başına en fazla iki model çağrısı" diyor ve şema
yeniden denemesini ayrı bir paragrafta anlatıyor; ikisinin bileşimini söylemiyor. `diagnostics.modelCalls`
handler düzeyini saydığı için tanılama 2 derken gerçekte 4 HTTP çağrısı olmuş olabilir; başarısız şema
denemelerinin tokenları da kullanım toplamına girmiyor.

**Öneri:** Üç düzeltme mekanizmasına (şema, alıntı, öncelik) ortak bir gerçek çağrı bütçesi koy veya README'de
"en fazla iki düzeltme turu; her turda şema düzeltmesi için bir ek deneme" diye netleştir. Başarısız denemelerin
tokenlarını da toplamaya ekle.

### 2. Orta: çelişki kaydındaki seçilen kaynak, cevabın atıflarıyla eşleştirilmiyor

Öncelik denetimi "kaybeden kaynağın seçilmesi" ve "atıfın kaybedene işaret etmesi"ni yakalıyor; ama model
çelişkide politikayı seçtiğini bildirirken cevabını **alakasız üçüncü bir dokümana** dayandırıyorsa hiçbir kontrol
tetiklenmiyor — cevap, politikayı seçtiğini söyleyen çelişki kaydıyla birlikte geçiyor. Ayrıca seçilen etiketi
bağlamda olmayan çelişki kayıtları sessizce atılıyor (`CheckConflicts`'ta `continue`); bu bilinçli bir tercih
olup kod açıklamasında belgelenmiş, ama "raporlanmış ama geçersiz referanslı" bir çelişki denetimsiz kalıyor.

**Öneri:** Cevapta raporlanan çelişkinin seçilen kaynağının kabul edilmiş atıflar arasında olmasını şart koş (ya da
bu iki kavramın kasıtlı olarak ayrı olduğunu sözleşmeyle yaz). Geçersiz referanslı çelişkileri düzeltme turuna
alıp ikincisinde reddetmeyi düşün.

### 3. Orta: sayısal koşulun yönü değerlendirmede korunmuyor

"750 TL altındaki siparişlerde kargo ücretsizdir." — beklenen kaynak, doğru bölüm, doğrulanmış alıntı ve
kaynağa dayalı sayılarla birlikte bu yanlış yanıt, N04'ün tüm mevcut kontrollerinden geçer. `mustNotContain`
yalnızca belirli olumsuz biçimleri yakalar. Kod içi açıklamalar bu sınırı kabul ediyor ve yanıtlar elle okunuyor;
ama otomatik katman ters kararı görmüyor.

**Öneri:** Kritik eşik sorularına (N04, iade süresi, garanti süresi) ters-yön ve farklı olumsuzluk örnekleri ekle;
değerlendiricinin bunları başarısız saydığını `EvalChecksTests` ile sabitle. Bu madde "tamamlandı" değil
"güçlendirildi, koşul yönü kısmi" olarak kalmalı.

### Bağımsız setteki iki başarısızlık — doğru biçimde raporlanmış

- **H03 gerçek bir yanlış ret:** Model, garanti süresi dolmuş bir cihaz için arıza nedeni gibi bilinmeyen
  ayrıntılar isteyip cevap vermiyor (over-refusal). Prompt kuralı 4'ün bu davranışı tam çözmesi beklenemez;
  README'de bilinen sınır olarak açıklanmış ve beklenti cevaba uydurulmamış. Bu, değerlendirme disiplini açısından
  doğru tercih.
- **H05 değerlendirmenin kendi yanlış başarısızlığı:** Gerçek yanıt ("adres değişikliği yapılamamaktadır") kaynakla
  uyumlu; kontrol yalnızca "yapılamaz/değiştirilemez/…" biçimlerini aradığı için kalmış. Kontrol genişletilirse bu
  değişikliği değerlendirme değişikliği olarak kaydet ve ilk 10/12 koşusunu eski ölçütle yapılmış gibi gösterme.
  Her iki durumda da setin "ayar için kullanılmadı" iddiası korunmuş olmalı; bu sete bakarak prompt/eşik ayarı
  yapılacaksa artık bağımsız değildir, sonraki doğrulama için yeni soru gerekir.

## Genel değerlendirme

İlk incelemedeki iki yüksek öncelikli temel güvenlik açığı (doğrulanmamış alıntı, zorlanmayan öncelik) artık kodla
değil, testle zorunan gerçek davranışlar. Sözleşme (required şema), alıntı doğrulaması (sıra + sözcük başı) ve
ölçüm altyapısı (çıkış kodları, ret sözleşmesi, bağımsız set) teslim edilebilir düzeyde. Projenin en güçlü yanı,
bu düzeltme turunun da gösterdiği gibi: iddia → kod → test → değerlendirme zincirinin kapalı olması.

Kalan üç bulgu, teslimi engellemeyen ama görüşmede sorulabilecek noktalar. Önerilen son sıra:

1. Çağrı bütçesi: ortak bütçe ya da README netleştirmesi + token toplamı (Kalan 1).
2. Çelişki–atıf tutarlılığı: seçilen kaynağın cevabın dayanağı olmasını şart koş veya sözleşmeyle ayır (Kalan 2).
3. N04'e ters-yön örnekleri; H05 kontrol düzeltmesini şeffaf kaydet (Kalan 3 + hold-out notu).
4. Bu raporu, `YAPILACAKLAR.md` ve `YENIDEN_INCELEME.md`'yi dalda commitle; üçü sürecin denetim izidir.
