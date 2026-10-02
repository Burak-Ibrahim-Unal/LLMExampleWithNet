# Plan uygulaması sonrası kontrol

2 Ekim 2026. İncelenen son commit: `0f8ab1d`. Kaynak kodunda değişiklik yapılmadı; bu rapor eklendi.

**Sonuç:** Planın ana düzeltmeleri uygulanmış. İlk incelemedeki boş/uydurma alıntı kabulü, eksik JSON'un ret sayılması, alıntı parçalarının sırası ve eval çıkış kodu sorunları için somut korumalar ve testler var. Kaynak önceliğinin zorlanması ve değerlendirme kontrolleri de geliştirilmiş; ancak bu iki başlıkta kalan kabul ölçütleri var. Yeni düzeltme turlarının birleşiminde çağrı sınırı da beklenenden farklı çalışıyor.

## Bu incelemede doğrulananlar

- `dotnet build SupportAssistant.slnx --no-restore`: 0 hata, 0 uyarı.
- `dotnet test --solution SupportAssistant.slnx --no-restore`: 183 başarılı, 0 başarısız, 0 atlanan.
- Yeni kodda handler yanıtın kaynaklarına yalnızca `QuoteVerified=true` atıfları alıyor; boş cevap için kullanılan alıntılar da bu kabul edilmiş listeden geliyor.
- Eksik zorunlu payload alanları `required` ile korunuyor; `{}` ve null liste öğeleri için regresyon testleri var.
- Alıntı parçaları ilerleyen kaynak konumlarıyla eşleştiriliyor; eski ters sıra sorunu giderilmiş.
- Eval artık HTTP 200 ve başarılı zarf arıyor; başarısız soru varsa çıkış kodu 1 veriyor.
- README sağlık ucunun erişilebilirliği ölçmediğini ve doğrulanmış alıntının bütün cevap iddialarını kanıtlamadığını açıklıyor.

Canlı model değerlendirmesi bu incelemede yeniden çalıştırılmadı. Depodaki güncellenmiş raporlar ana set için 16/16, bağımsız set için 10/12 gösteriyor. Kod ve belgeler bu sonuçları açık biçimde raporluyor.

## Kalan bulgular

### 1. Orta — Soru başına iki gerçek model çağrısı sınırı uygulanmıyor

Konum: `AskQuestionCommandHandler.cs` içindeki `MaxModelCalls` ve üretici çağrısı; `OpenAiCompatibleAnswerGenerator.cs` içindeki `MaxAttempts` ve `chatClient.GetResponseAsync`.

Handler en fazla iki kez `GenerateAsync` çağırıyor. Üretici ise her çağrıda JSON hatası için iki kez gerçek sohbet isteği yapabiliyor. İki düzeltme mekanizması birleşince dört `IChatClient` çağrısı oluşuyor. `ModelUsage.Calls` yalnızca başarılı dönen üretici çağrılarını saydığı için tanılamada iki görünüyor; başarısız ayrıştırma turlarının tokenları da toplama girmiyor.

Gerçek handler ve üreticiyi, programlanmış bir `IChatClient` ve geçici SQLite ile çalıştırdığım örnek:

```text
1. çağrı: {} → üretici JSON düzeltmesi
2. çağrı: geçerli JSON, uydurma alıntı → handler alıntı düzeltmesi
3. çağrı: {} → üretici JSON düzeltmesi
4. çağrı: geçerli JSON ve doğru alıntı → kabul
actual chat calls=4, diagnostics calls=2, answerable=True
```

Örnekte arama kapısı, bu iki katmanı bağımsız incelemek için düşük eşikle yapılandırıldı; üretim eşikleri değiştirilmedi. HTTP SDK'sının taşıma tekrarları bu sayıma dahil değil.

**Yapılacak:** JSON, alıntı ve öncelik düzeltmeleri için ortak gerçek üretim çağrısı bütçesi uygula. Gerçek çağrı sayısını ve tüm üretim turlarının kullanımını tanılamaya taşı. Aynı örneğin en fazla iki sohbet çağrısıyla sonuçlandığını gösteren bir akış testi ekle. Yalnızca README'yi dört çağrı olarak değiştirmek, ajan rehberindeki mevcut iki çağrı kuralını karşılamaz.

### 2. Orta — Çelişki kararındaki seçilen kaynak ile cevabın kaynakları eşleştirilmiyor

Konum: `AskQuestionCommandHandler.cs` içindeki `CheckConflicts`, `violatesPrecedence` ve cevap oluşturma.

Yeni kontrol, yanlış öncelik seçimini ve kaybeden bölümün kabul edilen atıflarda bulunmasını yakalıyor. Fakat çelişkide seçilen bölümün kabul edilen atıflarda gerçekten yer almasını şart koşmuyor.

Ek örnekte model politika/SSS çelişkisinde politikayı seçtiğini bildirdi; cevabı yalnızca farklı bir kargo dokümanına dayandırdı. Handler sonucu:

```text
answerable=True, conflict chosen=policy, sources=shipping
```

Bu, yapılacaklar listesindeki “seçilen kaynağın cevabın atıflarıyla tutarlı olması” ölçütünün hâlâ karşılanmadığını gösteriyor. Ayrıca bilinmeyen seçilen etiket veya kullanılabilir elenen etiketi olmayan çelişkiler sessizce atlanıyor; raporlanmış fakat geçersiz referanslı çelişki kontrol dışı kalabiliyor. Bu son durum kod yolundan görüldü; ayrı çalıştırma örneği yapılmadı.

**Yapılacak:** Cevapta raporlanan çelişki kararını kabul edilen kaynaklarla eşleştir. Geçersiz çelişki referanslarını başarıyla düzeltilmiş gibi yutma; sınırlı düzeltme veya ret uygula. İlgisiz bir çelişki kaydını cevapla ilişkilendirmeyeceksen bunu açık bir sözleşmeyle ayır. Kazananın alıntılanmadığı ve bilinmeyen etiketlerin kullanıldığı durumlar için test ekle.

### 3. Orta — N04'te koşulu ters anlatan cevap hâlâ başarılı sayılıyor

Konum: `eval/questions.json` içindeki N04; `EvalChecks.cs` içindeki ifade ve sayı kontrolleri.

Önceki `999 TL` ve `ücretsiz değildir` örneği artık yakalanıyor. Ancak koşulun yönü doğrulanmıyor. Beklenen kaynak, doğru bölüm ve doğrulanmış gerçek alıntıyla şu yanlış cevap mevcut kontrollerin tamamından geçti:

```text
750 TL altındaki siparişlerde kargo ücretsizdir.
N04 reversed threshold passes=True
```

Sayı kaynakta geçiyor, `ücretsiz` ifadesi bulunuyor ve yasak `ücretsiz değil` ifadesi yok. Eşiğin üstü/altı anlamı bu kontrollerle korunmuyor. Genel sayı kontrolü de sayıları bütün atıf yapılan dokümandan veya sorudan aldığı için bir sayının doğru koşulla kullanıldığını kanıtlamıyor.

**Yapılacak:** N04'e koşul yönünü tersine çeviren ve olumsuzluğu farklı ifade eden örnekler ekle; değerlendiricinin bu örnekleri başarısız saymasını sağla. İade süresi ve garanti gibi diğer kritik kararlar için de sayının varlığı ile doğru kullanımını ayır. Bu başlık “tamamlandı” yerine “güçlendirildi, koşul kontrolleri kısmi” olarak değerlendirilmeli.

## Bağımsız setteki iki başarısızlık

- **H03 gerçek yanlış ret:** Garanti süresi dolmuş olmasına rağmen model arıza nedeni gibi ek ayrıntılar isteyerek cevap vermemiş. Bu, README'de bilinen sınır olarak açıklanmış. Bağımsız setteki beklentiyi cevaba uydurmak yerine başarısızlığı korumak doğru bir değerlendirme yaklaşımı.
- **H05 değerlendirme kaynaklı yanlış başarısızlık:** Cevap `adres değişikliği yapılamamaktadır` diyor; kontrol yalnızca belirli başka biçimleri aradığı için başarısız saymış. Cevap, gösterilen kaynaklarla içerik bakımından uyumlu. Kontrol düzeltilirse bunu değerlendirme değişikliği olarak kaydet; ilk bağımsız koşunun 10/12 sonucunu silme veya eski koşuyu yeni ölçütle yapılmış gibi sunma.

Model veya prompt üzerinde H03'e bakarak ayar yapılacaksa bu set artık geliştirme için kullanılmış olur; sonraki bağımsız doğrulama için yeni sorular gerekir.

## Plan maddelerinin durumu

| Madde | Durum |
|---|---|
| 1. Boş/doğrulanmamış alıntıyı engelle | Uygulanmış; handler yalnızca doğrulanmış atıfları kullanıyor |
| 2. Kaynak önceliği ihlalini engelle | Ana akış uygulanmış; seçilen kaynak/atıf tutarlılığı ve geçersiz referanslar eksik |
| 3. Eksik JSON'u retten ayır | Uygulanmış; yeni regresyon testleri var |
| 4. Değerlendirmeyi güçlendir | Kısmen; sayı, alıntı, bölüm ve çelişki kontrolü var, koşul yönü hâlâ kaçabiliyor |
| 5. Alıntı parçalarının sırası | Uygulanmış |
| 6. Eval başarısızlık kodu | Uygulanmış |
| 7. Bağımsız soru seti | 12 soru ve ayrı rapor mevcut; ayar için kullanılmadığı belgelenmiş, geçmiş bağımsızlığı sadece dosyalardan kesin kanıtlanamaz |
| 8. Son testler/gerçek model raporları | 183 test yeniden geçti; güncel kayıtlı raporlar mevcut, canlı koşu tarafımdan tekrarlanmadı |
| 9. README güvenceleri | Büyük ölçüde hizalı; iki gerçek model çağrısı iddiası düzeltilmeli |
| 10. Yeniden üretilebilir teslim | Temiz model kurulumu tarafımdan denenmedi; model edinme bağlantıları, kesin sunucu sürümü, donanım ihtiyacı ve tam alternatif sohbet modeli ayarı açıklamaları hâlâ güçlendirilebilir |
| 11. Görüşme hazırlığı | Koddan doğrulanamaz; demo ve sözlü açıklama hazırlığı gerekiyor |
| 12. Health anlamı | README'de netleştirilmiş; ayrı canlı erişim kontrolü eklemek şart değil |
| 13–14. Eşik izleme/üretim kapsamı | Bilinen sınırlar korunmuş; teslim için ek altyapı zorunluluğu yok |

**Önerilen son sıra:** Ortak gerçek çağrı bütçesi → çelişki/atıf tutarlılığı → ters koşul değerlendirmesi → H05 kontrolünün kayıtlı ve şeffaf düzeltmesi → testler ve raporların yenilenmesi. Önceki incelemedeki yüksek öncelikli temel hatalar önemli ölçüde giderilmiş; kalan bu maddeler tamamlanmadan planın tüm kabul ölçütlerinin karşılandığını söylemek doğru olmaz.
