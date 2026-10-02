# Üç düzeltmenin doğrulama raporu

İnceleme tarihi: 2 Ekim 2026. İncelenen commit: `e453a7b`. Kapsam: önceki incelemedeki üç açık ve bunlara ilişkin regresyonlar. Kaynak kodu değiştirilmedi.

**Sonuç: Önceki üç hata örneği artık engelleniyor.** Ortak model çağrısı bütçesi, çelişki kararının kullanılan kaynaklarla tutarlılığı ve N04'ün ters koşul kontrolü uygulanmış. Aşağıdaki sonuçlar bu incelemede yeniden çalıştırıldı.

## Derleme ve testler

| Komut | Sonuç |
|---|---|
| `dotnet build SupportAssistant.slnx --no-restore -v quiet` | Başarılı; 0 hata, 0 uyarı |
| `dotnet test --solution SupportAssistant.slnx --no-restore` | 288 başarılı, 0 başarısız, 0 atlanan |

## 1. Ortak gerçek model çağrısı bütçesi — doğrulandı

Handler üreticiye kalan bütçeyi `MaxModelCalls - usage.Calls` olarak iletiyor. Üretici JSON düzeltme denemelerini bu bütçeyle sınırlıyor; `GeneratedAnswer.Attempts` gerçek sohbet çağrısı sayısını taşıyor. Handler sayacı bu değeri kullanıyor. Başarılı dönen üretim akışında önceki JSON denemelerinin token kullanımları da toplanıyor.

Gerçek handler, gerçek üretici, programlanmış `IChatClient` ve geçici SQLite ile önceki senaryo yeniden çalıştırıldı:

```text
1. sohbet çağrısı: {}
2. sohbet çağrısı: geçerli JSON fakat uydurma alıntı
Sonuç: actual chat calls=2, diagnostics calls=2, answerable=False
```

Üçüncü ve dördüncü sohbet çağrısına geçilmiyor. Bu, bütçe dolduğunda doğru alıntıya ulaşmak için ek çağrı yapmak yerine güvenli ret verilmesi anlamına geliyor. Gerçek handler/üretici birlikte çalıştırılan yeni regresyon testi de çağrı sayısını ve toplam girdi tokenlarını kontrol ediyor. Başka bir test alıntı düzeltme turuna yalnızca kalan tek çağrının verildiğini doğruluyor.

**Sınır:** Bu sayım uygulamanın `IChatClient` üzerinden başlattığı üretim isteklerini kapsar. HTTP SDK'sının taşıma seviyesindeki tekrarları da DI yapılandırmasında kapatılmıştır (`maxRetries: 0`). Başarısız üretici çağrıları 502/503 hata zarfına gittiği için bu hataların bütün kullanım bilgisinin cevap DTO'sunda gösterildiği sonucu çıkarılmamalı.

## 2. Çelişkide seçilen kaynağın kullanılmasını denetleme — doğrulandı

Handler, öncelik kuralının kazanan dokümanının kabul edilen atıflarda bulunmasını kontrol ediyor. Eksiklikte `WinnerNotCited` geri bildirimiyle düzeltme istiyor. Kaybeden kaynaklar gerektiğinde bağlamdan çıkarılıyor. Geçersiz çelişki referansları artık sayılıyor ve düzeltme/ret yoluna giriyor.

Önceki örnek yeniden çalıştırıldı: model politika/SSS çelişkisinde politikayı seçtiğini söylüyor, fakat yalnızca başka bir kargo dokümanını alıntılıyor. Sorun ikinci denemede de sürdüğünde:

```text
answerable=False
refusalReason=UnresolvedConflict
sources=[]
```

Ek birim testleri düzeltme turunda kazananın gerçekten alıntılanmasıyla başarılı cevap üretimini, eşit öncelikli kaynaklarda geri bildirimi ve geçersiz etiketleri kapsıyor.

**Sınır:** Kazananla atıf tutarlılığı doküman kimliği düzeyinde kontrol ediliyor. Bu, farklı bir dokümana dayanıp başka dokümanı seçilmiş gösterme sorununu kapatıyor; aynı dokümanın yanlış bölümünü alıntılama veya cevap cümlesinin anlamca kaynaktan çıkıp çıkmadığı için genel bir anlamsal doğrulama sağlamıyor. Bildirilmeyen çelişkinin tespiti hâlâ modele bağlı.

## 3. Ters koşullu cevabın N04'ten geçmesi — doğrulandı

Beklenti sözleşmesine `Conditions` eklenmiş ve N04'ün ücretsiz kargo eşiğini doğru yönde ifade etmesi zorunlu kılınmış. Yasak ters karar ifadeleri de genişletilmiş.

Doğru doküman, doğru bölüm, doğrulanmış gerçek alıntı ve kaynakta bulunan sayı ile önceki yanlış cevap yeniden değerlendirildi:

```text
Cevap: 750 TL altındaki siparişlerde kargo ücretsizdir.
N04 reversed threshold passes=False
```

Yeni regresyonlar gerçek soru dosyasındaki beklentileri kullanıyor; farklı ters koşul ve olumsuz karar biçimlerinin başarısız, doğru alternatif yazımların başarılı sayılması test edilmiş. Kontroller yalnızca ayrı bir test beklentisi üzerinde çalışmıyor.

**Sınır:** `Conditions` yine ifade eşleşmesine dayanıyor; bütün Türkçe yazımları veya birbirini çürüten tüm cümle birleşimlerini anlam düzeyinde doğrulamıyor. Belirtilen hata örneği ve test edilmiş varyantları kapanmış durumda. README'deki değerlendirme sınırlarının korunması uygun.

## Değerlendirme raporları ve kapsam

Canlı LLM/embedding değerlendirmesi bu incelemede yeniden çalıştırılmadı; depodaki raporlar incelendi. Güncel ana set raporu ve düşünme modu açık raporu 16/16 gösteriyor. Bağımsız setin ilk raporu korunmuş; `holdout-rerun` raporu da 10/12 gösteriyor. Kontrol sonuçları programlanmış model çıktılarıyla hata yollarının çalıştırılmasına ve 288 testin yeniden geçmesine dayanıyor. Bunlar gerçek modelin hata sıklığına dair yeni bir ölçüm değildir.

Depoda güvenlik ve diğer alanlarda ek değişiklikler de var. Bu rapor onların tamamına ilişkin kapsamlı bir güvenlik incelemesi veya projenin tüm olası hatalardan arındırıldığı iddiası değildir.

**Karar:** Önceki üç bulgu, doğruladığımız kabul örnekleri bakımından kapatılabilir. Bu kapsamda yeni bir teslim engeli saptanmadı. Son teslimde güncel test sayısını ve kullanılan değerlendirme raporlarının hangi kod/soru setiyle üretildiğini açık tut.
