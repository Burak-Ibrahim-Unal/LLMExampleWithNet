# SupportAssistant — Ajan Rehberi

Bu dosya, kodlama ajanlarının (ve geliştiricilerin) bu depoda mevcut mimariden sapmadan çalışması için
kuralları tanımlar. Soyut tavsiye değil, depodaki gerçek yapı esas alınır.

## 1) Depo haritası

- `src/Shared/Shared.Kernel` — `EntityBase`, `IRepository<T>`, `ISoftDeletable` (hiçbir framework bağımlılığı yok)
- `src/Shared/Shared.Application` — `ApiResult<T>`, `Messages`, migrator/seeder arayüzleri
- `src/Shared/Shared.Infrastructure` — `AppDbContext`, `EfRepository<T>`, `DbMigrator`
- `src/Modules/Knowledge/Knowledge.Domain` — `KnowledgeDocument`, `DocumentChunk`, `QuestionLog`, repository arayüzleri
- `src/Modules/Knowledge/Knowledge.Application` — komut/sorgu + handler'lar, `KnowledgeBusinessRules`, portlar
  (`IKnowledgeIndex`, `ITextEmbedder`, `IGroundedAnswerGenerator`, `IKnowledgeBaseSource`), cevaplama politikaları (`Answering/`)
- `src/Modules/Knowledge/Knowledge.Infrastructure` — EF konfigürasyonları, markdown ingest, BM25/vektör/RRF indeksi, LLM ve embedding adaptörleri
- `src/Services/Knowledge/Knowledge.Service` — `IKnowledgeService` (MediatR facade)
- `src/API/SupportAssistant.API` — FastEndpoints endpoint'leri, DI toplama (`Extensions/`), `Program.cs`
- `knowledge-base/` — bilgi tabanı (tek doğruluk kaynağı); `eval/` — değerlendirme seti ve sonuçları
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

## 4) Bilgi tabanı sözleşmesi

- Her dosya YAML front matter ile başlar: `id, documentKey, title, version, effectiveDate (yyyy-MM-dd), status (active|superseded), supersedes, category (politika|prosedur|kilavuz|sss)`.
- Aynı prosedürün sürümleri aynı `documentKey`'i paylaşır; bölüm başlıkları (`##`/`###`) sürümler arasında hizalı tutulur.
- Chunk = başlık altındaki bölüm; bölüm yolu (`"2. Destek Seviyeleri > 2.2 Seviye 2 (L2)"`) atıflarda görünür.
- Dosya değişince `POST /v1/documents/reindex` yeter; yalnızca değişen dokümanlar yeniden embed edilir.

## 5) Cevaplama kuralları (değiştirirken korunacak)

- Sürüm çelişkisi kodda (`VersionResolver`) çözülür; eski sürüm modele hiç gönderilmez.
- "Bilgi yok" üç kapılıdır: arama kanıtı (`AnswerabilityPolicy`) → modelin `answerable` kararı → atıf doğrulama (`CitationValidator`).
- Modelin çıktısı JSON şemasıyla kısıtlanır; şemada nullable alan kullanma (llama.cpp grammar uyumu).
- Prompt değişikliği yapınca `tools/SupportAssistant.Eval` ile değerlendirmeyi yeniden koş ve raporu güncelle.

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
