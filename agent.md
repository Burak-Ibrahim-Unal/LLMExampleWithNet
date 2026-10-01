# AdliKolay Architecture Agent Guide

Bu dosya, AdliKolayFull kod tabaninda halihazirda kullanilan mimariyi tekrar uretmek ve korumak icin operasyonel kurallari tanimlar.
Soyut tavsiye degil, mevcut projedeki gercek uygulama patternleri esas alinir.

## 1) Amaç

- Yeni projelerde bu mimariyi ayni sekilde kur.
- Mevcut projede yeni feature gelistirirken mevcut cizgiden sapma.
- Mimari tutarliligi kod, klasor, bagimlilik ve veri akisi seviyesinde koru.

## 2) Kaynak Gercekleri (Bu Repoda Ne Var)

- Backend: `src/API`, `src/Shared`, `src/Modules`, `src/Services`
- Frontend web: `frontend/web` (Next.js + React Query + Zustand + AuthContext)
- Frontend mobile: `frontend/mobile` (Expo/RN + React Query + Zustand + AuthContext)
- Ortak API type contract: `shared/types/api.types.ts`
- Endpoint altyapisi: FastEndpoints, global route prefix `v1`
- Standart response envelope: `ApiResult<T>`

## 3) Klasor ve Modul Sozlesmesi

### 3.1 Shared katmani

- `src/Shared/Shared.Kernel`
  - `EntityBase`, `ISoftDeletable`, `IRepository<T>`, core entity/value types
- `src/Shared/Shared.Application`
  - `ApiResult<T>`, query/command contractlari, migration/seeding arayuzleri
- `src/Shared/Shared.Infrastructure`
  - `AppDbContext`, `EfRepository<T>`, seeder/migrator implementasyonlari

### 3.2 Modul katmani (`src/Modules`)

- Full modul (Domain/Application/Infrastructure ayrimi) ornekleri:
  - `Cases`, `Clients`, `Documents`, `Hearings`, `PersonalFinance`, `Petitions`, `Tasks`, `Transactions`, `Blog`
- Daha sade modul ornekleri:
  - `Announcements`, `Auth`, `Subscription`, `Mobile`

### 3.3 API katmani

- Endpointler feature bazli:
  - `src/API/Endpoints/Cases/*Endpoint.cs`, `Tasks/*Endpoint.cs` vb.
- DI toplama:
  - `src/API/Extensions/ModuleServiceExtensions.cs`
  - `src/API/Extensions/InfrastructureServiceExtensions.cs`
  - `src/API/Extensions/WebApiServiceExtensions.cs`

### 3.4 Service katmani

- `src/Services/*`
- Cogu feature servisi MediatR facade:
  - `CaseService`, `TaskService`, `ClientService` vb.
- Bazi servisler repository merkezli:
  - `BlogService`, `AnnouncementService`, `AuthService`

## 4) Katman Bagimlilik Kurallari (Zorunlu)

Mimari testler (`tests/AdliKolay.ArchTests`) bu kurallari dogrular:

- `Shared.Kernel`:
  - `Microsoft.EntityFrameworkCore` bagimliligi OLAMAZ
  - `Shared.Infrastructure` bagimliligi OLAMAZ
  - `Shared.Application` bagimliligi OLAMAZ
  - `Microsoft.Extensions` bagimliligi OLAMAZ
- `Shared.Application`:
  - `Shared.Infrastructure` bagimliligi OLAMAZ
- API uygulama kodu (Program/Extensions haric):
  - `Shared.Infrastructure` / `AppDbContext` dogrudan referanslayamaz

Kurala aykiri tasarim yapma. Gerekirse once mimari karar al, sonra testi bilincli guncelle.

## 5) Backend Uygulama Kalibi

### 5.1 Endpoint contract

- Endpoint class adi: `*Endpoint`
- Base type: `Endpoint<TRequest, ApiResult<TResponse>>` veya `EndpointWithoutRequest`
- Route: kisa feature path (`Post("cases")`, `Get("tasks")`), prefix otomatik `v1`
- Donus: her zaman `ApiResult<T>` envelope

### 5.2 Kimlik ve abonelik akisi

- Global pre-processorlar:
  - `UserIdPreProcessor` -> `HttpContext.Items["CurrentUserId"]`
  - `SubscriptionPreProcessor` -> `HttpContext.Items["UserPlan"]`
- Bircok endpoint halen explicit `UserClaims.GetUserId(User)` kontrolu yapar. Bu pattern korunur.
- Kisitli plan create islerinde quota kontrolu patterni kullanilir:
  - `ISubscriptionQuotaService.ExceedsCreateQuotaAsync(...)`

### 5.3 CQRS + MediatR

- CRUD agirlikli featurelerde akisi su sekilde kur:
  - Endpoint -> `I<Feature>Service` -> MediatR command/query -> handler -> repository
- Handler siniflari module `Application` altinda tutulur.

### 5.4 Business Rules katmani (zorunlu)

Bu projede business rules soyut bir kavram degil, kodda net bir katman olarak var:

- Konum: `src/Modules/<Feature>/Application/BusinessRules/<Feature>BusinessRules.cs`
- DI kaydi: `src/API/Extensions/ModuleServiceExtensions.cs` icinde `AddScoped<<Feature>BusinessRules>()`
- Kullanim: handler constructor'inda rule sinifi inject edilir ve `Handle(...)` icinde ilk adimlarda cagrilir.
- Donus sekli: cogu metod `ApiResult<T>?` dondurur, hata varsa `ApiResult.Fail(...)`, yoksa `null`.
- Uygulama bicimi: handler "fail-fast" calisir.
  - `var err = _rules.CheckXxx<Resp>(...); if (err != null) return err;`

Rule siniflari su an mevcut:

- `CaseBusinessRules`
- `ClientBusinessRules`
- `TaskBusinessRules`
- `DocumentBusinessRules`
- `HearingBusinessRules`
- `PersonalFinanceBusinessRules`
- `PetitionBusinessRules`
- `TransactionBusinessRules`
- `AuthBusinessRules`
- `SubscriptionBusinessRules`

### 5.5 Rule tasarim kalibi (bu repoda kullanilan)

- Business rule siniflari "stateful service" degil; ince/granuler dogrulama metotlari.
- Rule sinifi repositoryye baglanabilir (ownership/existence/duplicate kontrolleri icin).
- Handlerlar rule disinda sadece orchestration yapar:
  - input normalize et
  - rule cagir
  - entity olustur/guncelle
  - repository save et
- Rule metotlari teknik exception firlatmak yerine `ApiResult` ile kontrollu hata dondurur.
- Hata kodlari ve mesajlari sistemdeki ortak yapiyla uyumlu olur:
  - code ornekleri: `NotFound`, `Duplicate`, `ValidationError`, `Forbidden`, `BadRequest`
  - title/message kaynagi: `Messages.*`

### 5.6 Modul bazli business rule katalogu (mevcut gercek kullanim)

- Cases:
  - `CheckCaseTypeAsync`
  - `CheckDuplicateAsync` (caseNo + unitName + user)
  - `CheckCaseFound`
  - `CheckCanRestore`
- Clients:
  - `CheckDuplicateAsync` (fullName + user)
  - `CheckClientFound`
  - `CheckCanRestore`
- Tasks:
  - `CheckDuplicateAsync` (title + user)
  - `CheckTaskFound`
  - `CheckCanRestore`
  - `CheckReminderBeforeDue`
  - `CheckClientOwnershipAsync`
  - `CheckStatusTransition`
- Documents:
  - `CheckDuplicateAsync` (title + user)
  - `CheckDocumentFound`
  - `CheckCanRestore`
  - `CheckClientOwnershipAsync`
  - `CheckCaseOwnershipAsync`
- Hearings:
  - `CheckDuplicateAsync` (unitName + caseNo + date + user)
  - `CheckHearingFound`
  - `CheckCanRestore`
- PersonalFinance:
  - `CheckOwnershipAsync`
  - `CheckEntryFound`
  - `CheckCanRestore`
- Petitions:
  - `CheckOwnershipAsync`
  - `CheckPetitionFound`
- Transactions:
  - `CheckPaymentTypeAsync`
  - `CheckClientOwnershipAsync`
  - `CheckTransactionFound`
  - `CheckCanRestore`
  - `CheckAmountPositive`
  - `CheckClientRequiredForModuleType`
- Auth:
  - `CheckUserFound`
  - `CheckCanResendVerification`
  - `CheckAlreadyVerifiedForVerify`
  - `CheckVerificationCodeState`
  - `CheckVerificationCodeMatch`
  - `CheckProfileImageExists`
- Subscription:
  - `CheckUserFound`
  - `CheckPremiumPurchaseEligibility`
  - `CheckPremiumDurationRange`
  - `CheckGrantDurationPositive`

### 5.7 Endpoint seviyesinde kalan business kurallar

Tum is kurallari `*BusinessRules` icinde degil; projede endpoint seviyesinde kalan alanlar da var:

- Subscription quota kurallari create endpointlerinde:
  - `SubscriptionPreProcessor` + `ISubscriptionQuotaService.ExceedsCreateQuotaAsync(...)`
- Authenticated kullanici dogrulamasi:
  - Global pre-processor olsa da endpointte `UserClaims.GetUserId(User)` tekrarli kontrol patterni var.

Bu iki pattern projede aktif kullanildigi icin korunur.

### 5.8 Persistence kalibi

- Tek DbContext: `AppDbContext`
- Modul entity mappingleri `IEntityTypeConfiguration<T>` ile yazilir.
- Cok modullu config kaydi:
  - `InfrastructureServiceExtensions.AddInfrastructureServices(...)`
  - `ConfigurationAssemblies` listesine module assembly eklenir.
- Soft-delete pattern:
  - Entity `ISoftDeletable` uygular (`DeletedAtUtc`)
  - EF config `HasQueryFilter(x => x.DeletedAtUtc == null)` ekler
  - Restore endpoint/command patterni bulunur.

### 5.9 Standart response ve status

- Basari/hatada `ApiResult<T>` kullan.
- Endpointte status kodu net set edilir:
  - `result.WithStatus(200/201/400/404/403...)`

## 6) Modul Bagimlilik Pratigi (Mevcut Gercek Durum)

Mevcut cross-module referanslar:

- `Cases -> Clients`
- `Documents -> Clients, Cases`
- `Tasks -> Clients, Cases`
- `Transactions -> Clients, Cases`
- `Petitions -> Clients, Cases`
- `Hearings -> Cases`
- `Subscription -> Auth`
- `Mobile (backend module) -> Auth`

Yeni module eklerken gereksiz capraz bagimlilik acma. Once shared contract ile cozmeyi dene.

## 7) Frontend Veri ve State Sozlesmesi

### 7.1 Ortak prensip

- API type source of truth: `shared/types/api.types.ts`
- Web ve mobile API clientlari merkezi:
  - `frontend/web/src/lib/api.ts`
  - `frontend/mobile/src/api.ts`
- Server state React Query ile yonetilir.
- UI/local state ihtiyacinda Zustand kullanilir.

### 7.2 Web pattern

- Query key factory: `frontend/web/src/queries/queryKeys.ts`
- Query helperlar: `assertResult`, `requireToken`, `authAwareRetry`, `flattenFilters`
- Auth akisi:
  - `AuthContext` (token lifecycle + refresh)
  - `AuthStoreSync` ile context -> zustand sync
- Provider girisi:
  - `frontend/web/src/app/providers.tsx` (QueryClientProvider)

### 7.3 Mobile pattern

- Query helper/key yapisi web ile paralel.
- Auth lifecycle ana merkez:
  - `frontend/mobile/src/auth-context.tsx`
- API client request wrapper:
  - timeout, error normalize, app version ve device id headerlari

## 8) Naming ve Kod Organizasyonu Kurallari

- Backend class suffixleri:
  - `*Endpoint`, `*Command`, `*Query`, `*Handler`, `*Service`, `*Repository`, `*Configuration`
- Module isimleri domain bazli ve tekil/okunakli:
  - `Cases`, `Clients`, `Tasks` vb.
- Frontend:
  - Query hook: `useXxx...`
  - Store: `xxxStore.ts`
  - Web page route klasorleri feature bazli (`davalar`, `muvekkiller` vb.)

## 9) Tekrarlayan Implementasyon Patternleri (Korunacak)

- Endpointte user id kontrolu + unauthorized response
- Create endpointlerinde restricted-plan quota check
- Handler icinde fail-fast business rule zinciri
- Soft-delete + restore endpoint ciftleri
- React Query keyleri icin filter flatten ve stabil key uretimi
- API katmaninda merkezi request wrapper (timeout + auth/error standardizasyonu)

## 10) Yeni Feature Ekleme Akisi

### 10.1 Backend feature checklist

1. `src/Modules/<Feature>` olustur (gerekliyse `Domain/Application/Infrastructure` ayir).
2. Entity(ler)i `EntityBase`/`ISoftDeletable` patternine gore yaz.
3. EF configuration ekle (`ToTable`, index, query filter).
4. Repository interface + implementation yaz.
5. `Application/BusinessRules/<Feature>BusinessRules.cs` ekle.
6. Rule metotlarini `ApiResult<T>?` fail-fast patterniyle yaz.
7. Command/query + handler yaz (MediatR):
   - handlerda business rule kontrollerini ilk bolume koy.
8. `src/Services/<Feature>/<Feature>Service.cs` ekle:
   - mevcut pattern gibi facade tut.
9. `src/API/Endpoints/<Feature>` endpointlerini ekle.
10. `ModuleServiceExtensions` icine DI kayitlarini ekle:
   - repository/service/businessrules kaydi
11. `InfrastructureServiceExtensions` icinde configuration assembly kaydini ekle.
12. Donusleri `ApiResult<T>` ile standardize et.

### 10.1.1 Business rule implementasyon checklist (zorunlu)

1. "Bulunamadi" kontrolu rule metoduna alin (`CheckXxxFound`).
2. "Duplicate" kontrolu varsa rule metodu yaz ve repositoryden cagir.
3. Ownership kontrolu gerekiyorsa rule metoduyla yap (`CheckClientOwnershipAsync` benzeri).
4. Restore patterni varsa `CheckCanRestore` ekle.
5. Modulde enum/transition kurali varsa rule metoduna koy (`CheckStatusTransition` benzeri).
6. Handlerda rule sirasi: ownership/existence -> validation -> duplicate -> persistence.
7. Rule mesajlari `Messages.*` uzerinden gelsin; hard-coded mesaj ekleme (mevcutla uyumlu istisnalar haric).

### 10.2 Web feature checklist

1. `shared/types/api.types.ts` kontratini guncelle.
2. `frontend/web/src/lib/api.ts` endpoint fonksiyonlarini ekle.
3. `frontend/web/src/queries/queryKeys.ts` key setini ekle.
4. `frontend/web/src/queries/hooks/use<Feature>*.ts` query/mutation hooklarini ekle.
5. Gerekli sayfa/componentte hook kullan, dogrudan daginik fetch ekleme.
6. Mutation sonrasi ilgili query keyleri invalidate et.

### 10.3 Mobile feature checklist

1. Ortak type kontratini kullan (`shared/types/api.types.ts`).
2. `frontend/mobile/src/api.ts` endpoint fonksiyonlarini ekle.
3. `frontend/mobile/src/queries/queryKeys.ts` ve `hooks/use<Feature>*.ts` ekle.
4. Screen seviyesinde hook kullan, API cagrilarini UI icine dagitma.

## 11) Mimari Tutarlilik Guardrail'lari

- API endpointten DbContext'e dogrudan inme; service + mediator/repository akisini kullan.
- `ApiResult<T>` disinda farkli response envelope uretme.
- Business kurali varsa handler icine dagitma; `Application/BusinessRules` altina tasi.
- Yeni global state acmadan once React Query ile cozulebilir mi kontrol et.
- Feature eklerken mevcut benzer featurei referans al:
  - Full backend referansi: `Cases`
  - Sade service referansi: `Blog` / `Announcements`

## 12) Karar Sirasi (Agent Operasyon Kurali)

Yeni bir is geldiginde su sirada karar ver:

1. Bu is hangi mevcut featurea benziyor?
2. Ayni klasor/sinif kalibini birebir koruyabiliyor musun?
3. Katman bagimlilik kuralini bozuyor musun?
4. Veri akisi (UI -> hook -> api -> endpoint -> service -> handler -> repo) tutarli mi?
5. Naming ve response standardi (`ApiResult`) korundu mu?

Bu 5 kontrolden gecmeyen implementasyonu tamamlanmis sayma.
