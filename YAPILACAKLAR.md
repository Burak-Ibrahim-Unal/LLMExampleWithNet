# Birleştirilmiş yapılacaklar listesi

Kaynak: teknik inceleme raporu ve ikinci incelemedeki geri bildirimler. Durumlar 2 Ekim 2026 tarihindeki son kontrole göre güncellendi. `[x]` yapılan ve kod, test veya teslim belgeleriyle desteklenen işleri; `[ ]` henüz tamamlandığı doğrulanmayan veya sonraya bırakılan işleri gösterir. Satır numaraları değişebileceği için dosya ve sınıf adları esas alınmıştır.

**Son doğrulama:** Derleme 0 hata ve 0 uyarı; 288/288 test başarılı. Önceki üç hata örneği artık engelleniyor. Ayrıntılar: [üç düzeltmenin kontrol raporu](UC_DUZELTME_KONTROL_RAPORU.md). Canlı model koşuları kayıtlı raporlardan doğrulandı; inceleme sırasında yeniden çalıştırılmadı. Ana set 16/16, bağımsız set 10/12; H03 yanlış ret ve H05 değerlendirme kaynaklı başarısızlık olarak belgelenmiş durumda.

Öncelikler: **P1** yanıt güvenilirliğinde teslim öncesi düzeltme; **P2** sözleşme ve değerlendirme güvenilirliği; **P3** küçük düzeltmeler ve açıklık. Üretim kapsamındaki geliştirmeler ayrıca ayrılmıştır.

## A. Teslim öncesi düzeltmeler

### 1. P1 — Doğrulanmamış ve boş alıntıları yanıtın dayanağı olmaktan çıkar

- [x] `CitationValidator` ve handler arasında yalnızca boş olmayan, doğrulanmış alıntıların cevap için kabul edilmesini sağla.
- [x] Hiç kabul edilebilir alıntı kalmazsa `NoValidCitations` ile açık ret ver. Yeniden üretim kullanılacaksa deneme sayısını sınırla.
- [x] Cevap temizlendikten sonra alıntıya dönülen yolu da aynı kurala bağla; doğrulanmamış metin cevaba dönüşmesin.
- [x] Mevcut “doğrulanmayan alıntıyı tut” testini yeni sözleşmeye göre güncelle; boş, uydurma ve karışık geçerli/geçersiz alıntılar için regresyon testleri ekle.
- [x] JSON, alıntı ve öncelik düzeltmeleri için ortak iki gerçek model çağrısı bütçesi uygula; tanılamada gerçek çağrı sayısını ve toplam token kullanımını göster.

**Kabul:** Kaynak `İade süresi 30 gündür.` iken `900 gün` veya boş alıntı, tek kaynak olarak başarılı cevabı destekleyemez. Kabul edilen diğer kaynakları bulunması, cevaptaki desteklenmeyen iddiaları otomatik olarak güvenli yapmaz; madde 4'te ayrıca değerlendirilmeli.

Dosyalar: `CitationValidator.cs`, `AskQuestionCommandHandler.cs`, ilgili birim testleri.

### 2. P1 — Bildirilen kaynak önceliği ihlalinde cevabı durdur

- [x] `ruleSatisfied=false` içeren model çıktısını başarılı cevap olarak döndürme; açık ret veya sınırlı yeniden üretim uygula.
- [x] Çelişkide seçilen kaynağın gerçekten cevabın atıflarıyla tutarlı olduğunu kontrol et.
- [x] Elenen çelişkili bölümün cevabın dayanağı olarak kullanılmasını engelle. Aynı dokümanın başka, çelişkisiz bölümlerini gereksiz yere yasaklama.
- [x] Politika yerine SSS seçimi, bilinmeyen etiketler ve çelişki/atıf uyumsuzluğu için test ekle; doğru seçim akışını koru.

**Kabul:** Model güncel politika yerine eski SSS'yi seçtiğini bildirirse bu çıktı başarılı cevap olarak kullanıcıya ulaşmaz. Modelin hiç bildirmediği çelişkileri bu kontrolün garantiyle yakalamadığı README'de belirtilir.

Dosyalar: `AskQuestionCommandHandler.cs`, `SourcePrecedence.cs`, cevap DTO'ları ve testleri.

### 3. P2 — Eksik model çıktısını gerçek bilgi yetersizliğinden ayır

- [x] Payload'ın zorunlu alanlarının gerçekten geldiğini doğrula; C# varsayılanlarını geçerli model kararı olarak kabul etme.
- [x] `{}`, eksik karar alanı, yanlış alan türü, null/geçersiz liste öğeleri gibi bozuk çıktıları düzeltme denemesine yönlendir.
- [x] Düzeltme de başarısızsa `InvalidOutput`/502 döndür; bunları `ModelInsufficientContext` olarak kaydetme.
- [x] Geçerli `answerable=false` çıktısının hâlâ açık ret olarak çalıştığını test et; şeması zorlanmayan yapılandırmayı da kapsa.

**Kabul:** `{}` gerçek bir ret sayılmaz; geçerli ret, bozuk çıktı ve sağlayıcıya erişememe ayrı sonuçlar üretir. `answerable=true` + boş cevap için mevcut koruma sürer.

Dosyalar: `AnswerPayload.cs`, `OpenAiCompatibleAnswerGenerator.cs`, üretici testleri.

### 4. P2 — Değerlendirmeyi yanlış olumlu sonuçlara karşı güçlendir

- [x] N04'te yalnızca `750` bulunmasını yeterli sayma; ücretsiz kargo kararını ve sorunun istediği eşik koşulunu birlikte değerlendir.
- [x] Sorunun istemediği ek ayrıntıları zorunlu hale getirmeden, yanlış sayı ve ters karar içeren çıktıları başarısız say.
- [x] Kritik diğer sorularda süre, birim, koşul ve olumlu/olumsuz kararı gözden geçir: özellikle iade süresi, para iadesi, garanti ve destek saatleri.
- [x] Yanıtlanan sorularda beklenen kaynak bölümünü ve alıntı doğruluğunu kontrol et.
- [x] C04 için çelişki kaydını, seçilen/elenen kaynağı ve `ruleSatisfied=true` sonucunu zorunlu kıl.
- [x] Cevapsız sorularda yalnızca `answerable=false` değil, açık ret metni ve boş kaynak listesini de kontrol et.
- [x] “Doğru alıntı + yanlış cevap” ve “doğru anahtar kelimeler + yanlış koşul” örnekleriyle değerlendiricinin kendisini test et.
- [x] Gerçek cevapları insan gözüyle kontrol et; anahtar kelime eşleşmesini anlamsal doğruluk garantisi olarak sunma.

**Kabul:** `750 TL üzerindeki siparişlerde kargo ücretsiz değildir; 999 TL alınır.` yanıtı N04'ten geçmez. Doğru kaynağı alıntılayan fakat iade süresini yanlış anlatan cevap da başarılı sayılmaz.

Dosyalar: `EvalChecks.cs`, `EvalModels.cs`, `eval/questions.json`, değerlendirme testleri.

### 5. P3 — Üç noktalı alıntılarda parça sırasını doğrula

- [x] Alıntı parçalarını kaynak içinde ilerleyen konumlarda ara; ters sıralı parçaları doğrulanmış sayma.
- [x] Gerektiğinde kelime sınırlarını denetle; bir sayı veya kelime parçası başka bir kelimenin içinde eşleşmesin.
- [x] Türkçe harf, noktalama ve büyük/küçük harf normalizasyonunun doğrulama anlamını belgele.
- [x] Ters sıra ve geçerli sıralı kısaltma için test ekle.

**Kabul:** `İade süresi 30 gündür.` için `30...iade` doğrulanmaz; kaynak sırasını koruyan gerçek kısaltma kabul edilir.

Dosyalar: `CitationValidator.cs`, ilgili testler.

### 6. P3 — Eval aracının başarısızlığı otomasyona bildirmesini sağla

- [x] Herhangi bir soru başarısızsa sıfırdan farklı çıkış kodu döndür.
- [x] HTTP durumunu ve zarfın `success` değerini değerlendirmeye kat; yalnızca `data` varlığına güvenme.
- [x] Sağlayıcı/API hatasını bilgi yetersizliği nedeniyle verilmiş doğru ret gibi sayma.

**Kabul:** Bütün sorular geçerse çıkış kodu 0; bir soru kalırsa veya beklenmeyen API hatası oluşursa sıfırdan farklı kod. Başarısız soru bilgisi raporda korunur.

Dosyalar: `tools/SupportAssistant.Eval/Program.cs`, değerlendirme modelleri ve kontrolleri.

## B. Düzeltmelerden sonra teslimi doğrulama

### 7. Yeni sorularla bağımsız değerlendirme yap

- [x] Eşik veya prompt ayarlamak için kullanılmamış ikinci bir soru seti oluştur.
- [x] Parafraz, Türkçe karaktersiz yazım, sayısal sınırlar, belgeye yakın cevapsız soru, kısmi cevap ve çok konulu soru örnekleri ekle.
- [x] Güncel SSS/politika çelişkisini ve yanlış karar üretimini ayrıca değerlendir.
- [x] İlk seti kalibrasyon, ikinci seti doğrulama olarak etiketle; ikinci sette ayar yaparsan bunun artık bağımsız olmadığını belirt.

**Kabul:** Beklenen ve gerçek cevaplar iki set için ayrı raporlanır; başarısız örnekler ve yanlış retler görünür kalır. Sadece bu seti geçirmek amacıyla TopK/eşik artırılmaz.

### 8. Gerçek sağlayıcıyla yeniden çalıştır ve raporları yenile

- [x] Değişen davranışlara uygun testleri çalıştır.
- [x] `dotnet build SupportAssistant.slnx` ve `dotnet test --solution SupportAssistant.slnx` sonuçlarını doğrula.
- [x] Canlı sohbet ve embedding sunucularıyla ana değerlendirmeyi ve yeni seti çalıştır.
- [x] Beklenen/gerçek karşılaştırmasını, ham JSON sonuçlarını, model/yapılandırma bilgisini ve gecikme ölçülerini güncelle.
- [x] Düşünme modu karşılaştırması teslimde tutulacaksa değişen prompt/kod ile yeniden ölç veya raporu önceki sürüme ait olarak etiketle.

**Kabul:** Son raporlar teslim edilen kod ve yapılandırmayla eşleşir. Test sayısı yeni testlere göre güncellenir; eski 145/145 ve 16/16 sonuçları yeni sürümün kanıtı olarak kullanılmaz.

### 9. README iddialarını gerçek güvencelerle hizala

- [x] “Doğrulanmış alıntı” ifadesini yeni alıntı sözleşmesine göre güncelle.
- [x] Aynı ailede deterministik sürüm seçimi ile farklı ailelerde modele bağlı çelişki tespitini ayrı açıkla.
- [x] Doğru JSON, geçerli kaynak ve doğrulanmış alıntının bütün cevap iddialarını tek başına garanti etmediğini belirt.
- [x] Ret, geçersiz model çıktısı, sağlayıcı hatası ve varsa yeni ret nedenleri için API örneklerini güncelle.
- [x] Değerlendirme başarısını “tanımlı kontrollerden geçen soru sayısı” olarak anlat; kalibrasyon geçmişini ve küçük set sınırını koru.

**Kabul:** README, API sözleşmesi ve son raporlar aynı davranışı tarif eder.

### 10. Çalıştırma ve teslim paketini yeniden üretilebilir hale getir

- [x] Denenmiş model dosyalarının edinilmesini, kullanılan sunucu sürümünü ve donanım beklentisini belirt.
- [ ] Donanımı uygun olmayan değerlendirici için denenmiş bir alternatif sağlayıcının tam örnek yapılandırmasını ekle; anahtarları örnek değerlerle göster.
- [x] README'ye küçük model ve bulut sağlayıcısı için tam örnek yapılandırmalar ekle. Üstteki madde alternatiflerin canlı olarak denenmesini de içerdiği için açık bırakıldı.
- [x] `.env.example`, kurulum ve değerlendirme komutlarının güncel olduğunu kontrol et.
- [ ] Teslim edilecek dosyalar ve Git değişikliklerinde gerçek API anahtarı bulunmadığını kontrol et; `.env` ve yerel veritabanı dışlamalarını koru.
- [ ] Temiz bir klasörden kurulumu dene; değerlendiricinin mevcut yerel ayarlarına veya veritabanına bağımlı olmadığını doğrula.

**Kabul:** README izlenerek temiz kurulumda dokümanlar indekslenir, örnek soru cevaplanır ve eval çalışır. FastAPI veya arayüz eklemek bu iş listesinin gereği değildir.

### 11. Görüşme için teknik açıklamaları hazırla

- [x] BM25 + vektör + RRF tercihinin ve ölçülen katkının açıklamasını hazırla.
- [x] SQLite/bellek içi indeks seçimini veri boyutuyla ilişkilendir.
- [x] CQRS ve katmanların bu projeye kattığı somut yararı açıkla; gereksiz yeni katman ekleme.
- [x] Sürüm seçimini, üç ret kapısını ve kalan model bağımlılığını birer örnekle göster.
- [ ] Normal, cevapsız ve çelişkili soru için kısa bir canlı demo akışı hazırla.

**Kabul:** Yaklaşık 20 dakikalık görüşmede kod akışını, teknik tercihleri, ölçüm sınırlarını ve bilinen eksikleri açıklayabilirsin.

**Durum:** Teknik açıklamalar README'de hazır; canlı demo hazırlığı ve sözlü anlatımın prova edilmesi henüz doğrulanmadı.

## C. İsteğe bağlı iyileştirmeler ve izlenecek noktalar

### 12. P3 — Health durumunun anlamını netleştir

- [x] `IsConfigured` değerinin erişilebilirlik olmadığını API/README'de belirt veya ayrı bir readiness kontrolü ekle.
- [ ] Readiness eklenirse dış çağrıları kısa süreli ve sınırlı tut; health uçlarını her istekte pahalı model üretimine bağlama.

**Durum:** Yapılandırma durumunu açıklama seçeneği uygulanmış. İkinci madde koşulludur; ayrı readiness eklenmediği için şu an uygulanacak bir iş değildir.

**Kabul:** Model sunucusu kapalıyken gösterilen durumun yalnızca yapılandırmayı ifade ettiği anlaşılır veya erişim arızası ayrı raporlanır.

### 13. Arama eşiklerini yeni verilerle izle

- [ ] Sürüm filtresinden önce hesaplanan kanıt skorlarının davranışını ve güncel sürümle ikame edilen bölümleri yeni sorularda ölç.
- [ ] Yeni terimlerin BM25 kapsamını düşürmesi, çok konulu sorular ve yanlış ret oranlarını izle.
- [ ] Ancak ölçülmüş ihtiyaç varsa eşikleri veya soru ayrıştırma davranışını değiştir; her değişiklikte tekrar değerlendir.

**Kabul:** Bu maddeler doğrulanmış yeni hata diye sunulmaz; ölçüm sonuçlarına göre ayrı işlere dönüşür.

### 14. Üretim gereksinimlerini ayrı tut

- [ ] Üretime taşınacaksa reindex için yönetici yetkisi, diğer uçlar için uygun kimlik doğrulama ve istek sınırlarını planla.
- [x] Reindex için yönetici anahtarı ve soru uçlarında hız sınırı ekle; davranışlarını belgele. Kullanıcı kimlik doğrulaması kapsam dışında olduğundan üstteki üretim maddesi bütünüyle tamamlandı sayılmadı.
- [ ] Veri büyürse indeks altyapısını, şema değişiklikleri ve kalıcı kayıtlar önem kazanırsa migrasyon yaklaşımını yeniden değerlendir.
- [ ] Tarihsel sorgu ve çok turlu sohbeti ancak ürün gereksinimi varsa ele al.

**Kabul:** Bunlar üç günlük ödevin tamamlanma şartı yapılmaz; README'de kapsam dışı sınırlar açık kalır.

## Korunacak güçlü taraflar

- Katman bağımlılıklarını zorunlu kılan mimari testler ve port/adaptör ayrımı.
- Aynı doküman ailesindeki deterministik sürüm seçimi ve eski sürümlerin model bağlamından çıkarılması.
- Embedding arızasında BM25'e dönüş, boyut uyuşmazlığı kontrolü, bozuk dosya için 422 ve başlangıç dayanıklılığı.
- Eşzamanlı reindex serileştirmesi ve değişmeyen dokümanların embeddinglerini koruma.
- BM25/hibrit arama isabetini ayrı ölçme ve gerçek çıktıları beklenen sonuçlarla birlikte saklama.
- README'deki kalibrasyon geçmişi ve bilinen sınırların açıklığı.

**Kalan işler:** 10. maddede alternatif sağlayıcının canlı denenmesi, teslim paketinde son anahtar kontrolü ve temiz kurulum; 11. maddede canlı demo hazırlığı. Madde 12'nin açıklama seçeneği tamamlandı. Madde 13 sürekli izleme, madde 14 ise üretim kapsamındaki işlerdir; teslim öncesi zorunlu düzeltmelerle karıştırılmamalıdır.
