# SupportAssistant — Ajan Rehberi

Bu dosya, kodlama ajanlarının (ve geliştiricilerin) bu depoda mevcut mimariden sapmadan çalışması için
kuralları tanımlar. Soyut tavsiye değil, depodaki gerçek yapı esas alınır.

## 1) Depo haritası

- `src/Shared/Shared.Kernel` — `EntityBase`, `IRepository<T>`, `ISoftDeletable` (hiçbir framework bağımlılığı yok)
- `src/Shared/Shared.Application` — `ApiResult<T>`, `Messages` (metinler `Common/Resources/messages.json`'da), migrator/seeder arayüzleri
- `src/Shared/Shared.Infrastructure` — `AppDbContext`, `EfRepository<T>`, `DbMigrator`
- `src/Modules/Knowledge/Knowledge.Domain` — `KnowledgeDocument`, `DocumentChunk`, `QuestionLog`, repository arayüzleri
- `src/Modules/Knowledge/Knowledge.Application` — komut/sorgu + handler'lar, `KnowledgeBusinessRules`, portlar
  (`IKnowledgeIndex`, `ITextEmbedder`, `IGroundedAnswerGenerator`, `IKnowledgeBaseSource`), cevaplama politikaları (`Answering/`),
  prompt injection dedektörü (`Security/`)
- `src/Modules/Knowledge/Knowledge.Infrastructure` — EF konfigürasyonları, markdown ingest, BM25/vektör/RRF indeksi, LLM ve embedding
  adaptörleri; dil modeline giden metinler `Llm/Prompts/answer-prompt.yaml`'da
- `src/Services/Knowledge/Knowledge.Service` — `IKnowledgeService` (MediatR facade)
- `src/API/SupportAssistant.API` — FastEndpoints endpoint'leri, DI toplama (`Extensions/`), HTTP korumaları (`Security/`), `Program.cs`
- `knowledge-base/` — bilgi tabanı (tek doğruluk kaynağı); `eval/` — soru setleri (kalibrasyon, iki bağımsız set,
  halüsinasyon seti) ve sonuçları
- `tests/` — birim + mimari testleri, API entegrasyon testleri; `tools/SupportAssistant.Eval` — değerlendirme aracı

## 2) Katman kuralları (mimari testlerle zorunlu)

`tests/SupportAssistant.UnitTests/Architecture` bu kuralları derlenmiş assembly'ler üzerinde doğrular:

- `Shared.Kernel`: EF Core, `Microsoft.Extensions`, `Shared.Application`, `Shared.Infrastructure` bağımlılığı OLAMAZ.
- `Shared.Application`: `Shared.Infrastructure` ve EF Core bağımlılığı OLAMAZ.
- `Knowledge.Domain`: Application/Infrastructure, EF Core, MediatR bağımlılığı OLAMAZ.
- `Knowledge.Application`: Infrastructure, EF Core, `OpenAI`, `Microsoft.Extensions.AI` bağımlılığı OLAMAZ
  (LLM ve veritabanı SDK'ları yalnızca Infrastructure'da; Application kendi portlarını tanımlar).
- API endpoint'leri Infrastructure, repository arayüzleri veya EF Core'a doğrudan erişemez; servis katmanını kullanır.

Kurala aykırı bir tasarım gerekiyorsa önce mimari kararı yaz, sonra testi bilinçli güncelle.

## 3) Akış kalıbı

`Endpoint → IKnowledgeService → MediatR komut/sorgu → Handler → BusinessRules → Repository / Port`

- Endpoint: `Endpoint<TRequest, ApiResult<TResponse>>`, kısa route (`Post("questions")`), prefix otomatik `v1`,
  yanıt `await Send.ResponseAsync(result, result.StatusCode, ct)`.
- Handler önce iş kurallarını fail-fast çalıştırır: `var error = rules.CheckXxx<T>(...); if (error is not null) return error;`
- İş kuralları `Application/BusinessRules/KnowledgeBusinessRules.cs` içinde, `ApiResult<T>?` döndürür, mesajlar `Messages.Knowledge.*`'dan gelir.
- Yanıt zarfı her zaman `ApiResult<T>`; durum kodu açıkça set edilir.

## 3a) Metin ve boyut kuralları (koruyucu testlerle zorunlu)

`tests/SupportAssistant.UnitTests/Architecture/CodeConventionTests` bunları denetler:

- Kullanıcıya dönen Türkçe metin koda yazılmaz: metin `Shared.Application/Common/Resources/messages.json`'a, belgeli
  erişim noktası `Messages`'a eklenir (anahtar = iç sınıf + özellik adı). Yer tutuculu metin çağıran tarafta
  `string.Format(CultureInfo.InvariantCulture, …)` ile doldurulur.
- Dil modeline giden metin koda yazılmaz: `Knowledge.Infrastructure/Llm/Prompts/answer-prompt.yaml`. Yeni bir parça
  eklenirse `AnswerPromptTexts`'e alanı ve doğrulaması (beklenen yer tutucular) da eklenir. Yapı işaretleri
  (`KAYNAKLAR:`, `Bölüm:`, `DÜZELTME:`, `SORU:`) değişirse `AnswerPrompt.StructureMarker` kalıbı da güncellenir.
- İstisnalar: `[GeneratedRegex]` kalıpları, arama durak sözcükleri gibi veri listeleri, İngilizce log şablonları ve
  programcı hatalarına ait İngilizce istisna mesajları.
- Bir C# dosyası en fazla 500 satırdır (`src/`, `tools/`, `tests/`). Aşan sınıf sorumluluğuna göre bölünür; test
  sınıfları konuya göre ayrılır, paylaşılan kurulum bir taban sınıfta ya da `using static` ile kullanılan bir
  yardımcıda durur.

## 4) Bilgi tabanı sözleşmesi

- Her dosya YAML front matter ile başlar: `id, documentKey, title, version, effectiveDate (yyyy-MM-dd), status (active|superseded), supersedes, category (politika|prosedur|kilavuz|sss)`.
- Aynı prosedürün sürümleri aynı `documentKey`'i paylaşır; bölüm başlıkları (`##`/`###`) sürümler arasında hizalı tutulur.
- Chunk = başlık altındaki bölüm; bölüm yolu (`"2. Destek Seviyeleri > 2.2 Seviye 2 (L2)"`) atıflarda görünür.
- Dosya değişince `POST /v1/documents/reindex` yeter; yalnızca değişen dokümanlar yeniden embed edilir.

## 5) Cevaplama kuralları (değiştirirken korunacak)

- Sürüm çelişkisi kodda (`VersionResolver`) çözülür; eski sürüm modele hiç gönderilmez.
- "Bilgi yok" üç kapılıdır: arama kanıtı (`AnswerabilityPolicy`) → modelin `answerable` kararı → atıf doğrulama (`CitationValidator`).
- Yanıtın dayanağı yalnızca alıntısı bölüm metninde doğrulanmış atıflardır; doğrulanamayan atıf kaynak listesine girmez.
- Kaynaklar arası öncelik kuralını sunucu zorlar (`SourcePrecedence.Losers`); kaybeden bölüm bağlamdan çıkarılır.
  Kuralın kazananına hiç atıf yapılmaması ve bağlamda olmayan çelişki kimlikleri de ihlaldir (düzeltme turu, sürerse
  `UnresolvedConflict`). Yanıtta yalnızca seçilen kaynağı yanıtın atıf yaptığı dokümanlardan biri olan çelişkiler gösterilir.
- Soru başına en fazla iki **gerçek** model isteği. Bütçe tek yerde, handler'da (`MaxModelCalls`) tutulur ve üreticiye
  her çağrıda kalanı verilir (`maxAttempts`); üreticinin şema yeniden denemesi de bu bütçeden düşer. Sınırı artırma,
  ikinci bir bütçe ekleme. `GeneratedAnswer.Attempts` gerçek istek sayısını, token alanları bütün isteklerin toplamını taşır.
- Modelin çıktısı JSON şemasıyla kısıtlanır; şemada nullable alan kullanma, her alan `required` olsun (llama.cpp grammar uyumu, eksik çıktı = geçersiz çıktı).
- Prompt değişikliği yapınca `tools/SupportAssistant.Eval` ile değerlendirmeyi yeniden koş ve raporu güncelle.
- `eval/questions.json` kalibrasyon setidir: bir kontrolün kendi hatası (doğru bir yanıtı kaldıran ifade listesi gibi)
  düzeltilebilir, ama düzeltme README'de gerekçesiyle yazılır.
- `eval/questions-holdout.json`, `eval/questions-holdout-2.json` ve `eval/questions-hallucination.json` bağımsız
  setlerdir: eşik, prompt ya da `TopK` ayarı için kullanma; beklentilerini sonuç görüp değiştirme. Yeni bir bağımsız
  set ilk koşudan önce commit'lenir; sonuçları olduğu gibi raporlanır. İlk koşuların raporlarını (`eval/results/holdout/`
  gibi) silme; yeni koşuları ayrı bir `--label` ile yaz. Soru kimlikleri bütün setlerde benzersizdir (birim testi denetler).
- Kritik kararlarda (eşik, süre, kapsam) değerlendirme beklentisi sayının varlığını (`mustContain`) ve koşulun yönünü
  (`conditions`, ters yazımlar `mustNotContain`'de) ayrı denetler. Bu kontroller kısmidir; README'de öyle anlatılır.

## 5a) Güvenlik kuralları (değiştirirken korunacak)

- Prompt'a giren her güvenilmez metin (doküman alanları, soru, modelin önceki alıntıları) `AnswerPrompt.Neutralize`'dan
  geçer. Kullanıcı mesajına yeni bir alan eklerken de bu kural geçerlidir.
- `PromptInjectionDetector` kalıplarını değiştirirken hem saldırı hem masum soru örneklerini test et: yanlış alarm gerçek
  bir müşteri sorusunu yanıtsız bırakır. Hangi kalıbın yakalandığı istemciye söylenmez, yalnızca loglanır. Temsilciler
  müşteri kayıtlarını ve sohbet dökümlerini yapıştırır: satır başındaki `Sistem:` / `Asistan:` gibi biçimler tek başına
  saldırı sayılmaz (prompt'ta zaten etkisizleştirilir); normalleştirme kesme işaretli ekleri ayrı sözcüğe böler
  ("Alexa'dan" → "alexa dan"), kalıp yazarken bunu hesaba kat.
- Model sunucusuna giden istekler yeniden denenmez (SDK `maxRetries: 0`); soru başına iki istek bütçesi ancak böyle
  gerçek sayıyı gösterir.
- Sohbet şablonu belirteçleri çalışan modelin şablonundan alınır (`/props`); model ailesi değişirse kalıp güncellenir.
- Sistem prompt'unun öncelik kuralı `AnswerPrompt.PrecedenceRule` sabitinde kalır: çıktı koruması
  (`SystemPromptLeakDetector`) o kuralı sızıntı saymaz.
- Yeni bir ret nedeni eklenirse `RefusalReasons`, `Messages`, değerlendiricinin `RefusalContract`'ı ve README'deki ret
  tablosu birlikte güncellenir.
- Yönetici işlemleri `AdminKeyPreProcessor` ile korunur; anahtar tanımlı değilse uç kapalı kalır (403). Anahtarlar ve
  sunucu adresleri koda, `appsettings` dosyalarına, raporlara ya da README'ye yazılmaz; yalnızca `.env` / ortam değişkeni.
- HTTP korumaları (`Security/`) `Program.cs`'te uç noktalardan önce bu sırayla çalışır: güvenlik başlıkları → istek
  boyutu → hız sınırı. Her ret aynı `ApiResult` zarfıyla döner.

## 6) Test ve doğrulama

- TDD: önce başarısız test, sonra kod. Gerçek LLM testlerde kullanılmaz (sahte `IGroundedAnswerGenerator` / `IChatClient`).
- `dotnet build SupportAssistant.slnx` → 0 uyarı (uyarılar hata sayılır).
- `dotnet test --solution SupportAssistant.slnx` → tümü yeşil.
- Davranışı modele bağlı değişiklikler: API'yi çalıştır, `dotnet run --project tools/SupportAssistant.Eval`.

## 7) Karar sırası

1. İş hangi mevcut akışa benziyor (soru, arama, ingest, doküman sorgusu)?
2. Aynı klasör/sınıf kalıbı korunuyor mu (`Commands/`, `Queries/`, `BusinessRules/`, `Abstractions/`)?
3. Katman kuralı bozuluyor mu (mimari testler)?
4. Yanıt `ApiResult<T>` ve `Messages.*` standardında mı?
5. Testler ve (gerekiyorsa) değerlendirme güncel mi?
6. README'ler (`README.md` ve `README.en.md`) birlikte güncellendi mi?

## 8) Belgeler

- README iki dillidir: `README.md` Türkçedir ve depo sayfasında açılır, `README.en.md` İngilizce karşılığıdır. Her
  değişiklikte ikisi birlikte güncellenir: aynı bölüm yapısı, aynı sayılar (test sayısı, değerlendirme sonuçları,
  sürümler), aynı örnekler. En üstteki dil bağlantıları (`**Türkçe** | [English](README.en.md)` /
  `[Türkçe](README.md) | **English**`) korunur.
- Türkçe README'deki örnek JSON'lar, API mesajları ve soru metinleri API'nin gerçek (Türkçe) çıktısıdır. İngilizce
  README bunları İngilizceye çevirir ve çeviri olduklarını belirtir. Kodun birebir eşleştirdiği literaller (prompt
  işaretleri, dedektör kalıpları, değerlendirmedeki ifadeler) özgün hâliyle ve İngilizce açıklamasıyla kalır.
- Halüsinasyona karşı sonraki adımlar kodda `TODO(halüsinasyon-1…5)` yorumlarıyla, uygulanacakları yerde işaretlidir.
  Bir madde uygulanınca yorumu kaldırılır ve iki README'deki "Halüsinasyona karşı önlemler" / "Hallucination
  safeguards" tablosu birlikte güncellenir.
