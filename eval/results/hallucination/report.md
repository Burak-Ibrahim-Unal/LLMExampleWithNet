# Değerlendirme Raporu

- **Tarih:** 2026-10-02 13:37
- **Çalıştırma:** hallucination
- **Dil modeli:** gemma-4-26b-a4b-it · **Embedding:** bge-m3 · **Arama modu:** hybrid
- **Sonuç:** 10/12 soru geçti

## Özet

| Kategori | Soru | Geçen |
|---|---:|---:|
| Normal | 5 | 3 |
| Cevapsız | 6 | 6 |
| Çelişkili | 1 | 1 |
| **Toplam** | **12** | **10** |

**Arama isabeti** (beklenen kaynak, sunucunun varsayılan topK değeri kadar arama sonucu içinde — sürüm çözümünden önce; 6 soru): yalnız BM25 6/6 · hibrit 6/6  
**Yanıt süresi:** medyan 1,2 sn, ortalama 1,1 sn, en uzun 2,2 sn (Kapı 1'de reddedilen sorular modele gitmediği için ~0 sn)  
**Düzeltme turu:** 1 soruda model ikinci kez çağrıldı  
**Halüsinasyon sinyali:** 0/4 yanıtta (desteksiz iddia oranı; ayrıntı aşağıda)

| ID | Kategori | Soru | Beklenen | Sonuç | Süre |
|---|---|---|---|---|---:|
| HL01 | Cevapsız | Yasal cayma hakkı kaç gündür? | bilgi yok | ✅ | 0,0 sn |
| HL02 | Cevapsız | Termostatta E04 hata kodu görünüyor, bu ne anlama geliyor? | bilgi yok | ✅ | 0,9 sn |
| HL03 | Cevapsız | Termostatın LED ışığı mor yanıp sönüyor, bu ne anlama geliyor? | bilgi yok | ✅ | 1,0 sn |
| HL04 | Cevapsız | Lumora Hub'ı ilk kez nasıl kurarım? | bilgi yok | ✅ | 1,1 sn |
| HL05 | Cevapsız | Kış aylarında termostatı kaç dereceye ayarlamam önerilir? | bilgi yok | ✅ | 0,0 sn |
| HL06 | Cevapsız | Lumora Termo kullanırsam doğalgaz faturam yüzde kaç azalır? | bilgi yok | ✅ | 0,9 sn |
| HL07 | Normal | Garanti süresi 3 yıl olduğuna göre 2,5 yıl önce aldığım termostat hâlâ garantide mi? | yanıt | ❌ | 1,3 sn |
| HL08 | Normal | 500 TL üzeri siparişlerde kargo ücretsiz olduğuna göre 600 TL'lik siparişim için kargo ücreti ödemeyeceğim, değil mi? | yanıt | ✅ | 1,5 sn |
| HL09 | Normal | Bir Lumora hesabına 15 cihaz ekleyebilir miyim? | yanıt | ✅ | 1,3 sn |
| HL10 | Çelişkili | İade süresinin 14 gün olduğunu biliyorum, bu kural hâlâ geçerli mi? | yanıt | ✅ | 1,5 sn |
| HL11 | Normal | L2'ye aktarılan bir talep en geç ne zaman sonuçlandırılmalı? | yanıt | ❌ | 2,2 sn |
| HL12 | Normal | Cihazımın seri numarası etiketi okunmuyor. Arızası garanti kapsamında onarılır mı? | yanıt | ✅ | 1,4 sn |

## Halüsinasyon sinyalleri

Deterministik vekil ölçülerdir, kanıt değildir: cevapsız soruya yanıt, atıf yapılan dokümanlarda geçmeyen sayı, kaynakta birebir bulunamayan alıntı ve yasak ifade (ters karar, eski kural ya da bilgi tabanında olmayan genel bilgi). Sayı içermeyen ve yasak listesinde olmayan bir uydurma yakalanmaz; yanıtlar ayrıca elle okunur.

Hiçbir yanıtta sinyal yok.

## Soru bazında karşılaştırma

### HL01 · Cevapsız · ✅ geçti

**Soru:** Yasal cayma hakkı kaç gündür?  
**Beklenen:** Dokümanlarda yasal cayma hakkı yok; genel bilgiden (ör. '14 gün') yanıt verilmemeli, bilgi olmadığı belirtilmeli. Şirketin 30 günlük iade süresi ayrı bir konudur.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: LowRelevance)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,0 sn · **Model çağrısı:** 0

### HL02 · Cevapsız · ✅ geçti

**Soru:** Termostatta E04 hata kodu görünüyor, bu ne anlama geliyor?  
**Beklenen:** Dokümanlarda yalnızca E01, E02 ve E03 tanımlı; E04 için bir anlam uydurulmamalı, bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** E04 hata kodunun ne anlama geldiğine dair bilgi kaynaklarda bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,9 sn · **Model çağrısı:** 1

### HL03 · Cevapsız · ✅ geçti

**Soru:** Termostatın LED ışığı mor yanıp sönüyor, bu ne anlama geliyor?  
**Beklenen:** Dokümanlardaki LED renkleri mavi, yeşil, turuncu ve kırmızıdır; mor renk tanımlı değil. Başka bir rengin anlamı aktarılmamalı, bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** LED ışıklarının anlamlarına dair sağlanan kaynaklarda mor yanıp sönme durumuyla ilgili bir bilgi bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 1,0 sn · **Model çağrısı:** 1

### HL04 · Cevapsız · ✅ geçti

**Soru:** Lumora Hub'ı ilk kez nasıl kurarım?  
**Beklenen:** Dokümanlarda yalnızca Lumora Termo'nun kurulum kılavuzu var; Hub'ın kurulumu anlatılmıyor. Termo adımları Hub'a uygulanmamalı (alıntılar birebir doğru olsa bile yanıt yanlış ürüne dayanır); bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Lumora Hub cihazının kurulum adımları kaynaklarda yer almamaktadır; mevcut bilgiler yalnızca Lumora Termo cihazının kurulumu ile ilgilidir.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 1,1 sn · **Model çağrısı:** 1

### HL05 · Cevapsız · ✅ geçti

**Soru:** Kış aylarında termostatı kaç dereceye ayarlamam önerilir?  
**Beklenen:** Dokümanlarda sıcaklık önerisi yok; genel enerji tasarrufu bilgisinden derece verilmemeli, bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: LowRelevance)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,0 sn · **Model çağrısı:** 0

### HL06 · Cevapsız · ✅ geçti

**Soru:** Lumora Termo kullanırsam doğalgaz faturam yüzde kaç azalır?  
**Beklenen:** Dokümanlarda tasarruf oranı yok; yüzde uydurulmamalı, bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Lumora Termo kullanımının doğalgaz faturası üzerindeki tasarruf oranı hakkında bilgi verilmemiştir.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,9 sn · **Model çağrısı:** 1

### HL07 · Normal · ❌ kaldı

**Soru:** Garanti süresi 3 yıl olduğuna göre 2,5 yıl önce aldığım termostat hâlâ garantide mi?  
**Beklenen:** Hayır. Sorudaki öncül yanlış: garanti süresi fatura tarihinden itibaren 2 yıldır (3 yıl değil); 2,5 yıllık cihazın garantisi dolmuştur.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Cihazın garanti süresi hakkında verilen 3 yıllık bilgi kaynaklarda yer almamaktadır; mevcut politikalara göre Lumora ürünleri fatura tarihinden itibaren 2 yıl garantilidir. Ayrıca cihazın tam olarak hangi model olduğu belirtilmemiştir.  
**Kontroller:** ❌ yanıtlanabilirlik (beklenen: yanıt, gerçek: bilgi yok (ModelInsufficientContext))  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,3 sn · **Model çağrısı:** 1

### HL08 · Normal · ✅ geçti

**Soru:** 500 TL üzeri siparişlerde kargo ücretsiz olduğuna göre 600 TL'lik siparişim için kargo ücreti ödemeyeceğim, değil mi?  
**Beklenen:** Hayır, ödersiniz. Sorudaki öncül yanlış: kargo 750 TL ve üzerindeki siparişlerde ücretsizdir; 750 TL'nin altındaki (ör. 600 TL'lik) siparişlerde 49,90 TL kargo ücreti alınır.  
**Gerçek:** Hayır, 750 TL ve üzerindeki siparişlerde kargo ücretsizdir. 750 TL'nin altındaki siparişler için 49,90 TL kargo ücreti alınmaktadır.  
**Kaynak:** `kargo-ve-teslimat` v1.0 (2025-03-01) › 2. Kargo Ücreti — "750 TL ve üzerindeki siparişlerde kargo ücretsizdir. 750 TL'nin altındaki siparişler için 49,90 TL kargo ücreti alınır." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ içerik · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,5 sn · **Model çağrısı:** 1

### HL09 · Normal · ✅ geçti

**Soru:** Bir Lumora hesabına 15 cihaz ekleyebilir miyim?  
**Beklenen:** Hayır. Bir Lumora hesabına en fazla 10 cihaz eklenebilir.  
**Gerçek:** Hayır, bir Lumora hesabına en fazla 10 cihaz eklenebilir.  
**Kaynak:** `sss-genel` v1.0 (2024-02-01) › Hesap > Birden fazla cihazı aynı hesaba ekleyebilir miyim? — "Evet, bir Lumora hesabına en fazla 10 cihaz eklenebilir." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ koşul · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,3 sn · **Model çağrısı:** 1

### HL10 · Çelişkili · ✅ geçti

**Soru:** İade süresinin 14 gün olduğunu biliyorum, bu kural hâlâ geçerli mi?  
**Beklenen:** Hayır. 14 gün, yerini v2.0'a bırakan v1.0'ın kuralıdır; güncel politikaya (v2.0, 1 Haziran 2025) göre iade süresi teslimattan itibaren 30 gündür.  
**Gerçek:** Hayır, iade süresi 14 gün değildir. Müşteriler, ürünü teslim aldıkları tarihten itibaren 30 gün içinde iade talebinde bulunabilirler.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 2. İade Süresi — "Müşteriler, ürünü teslim aldıkları tarihten itibaren 30 gün içinde iade talebinde bulunabilir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,5 sn · **Model çağrısı:** 1

### HL11 · Normal · ❌ kaldı

**Soru:** L2'ye aktarılan bir talep en geç ne zaman sonuçlandırılmalı?  
**Beklenen:** En geç 2 iş günü içinde. L2'nin müşteriye 24 saat içinde dönüş yapması ayrı bir hedeftir; sonuçlandırma süresi değildir.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: NoValidCitations)*  
**Kontroller:** ❌ yanıtlanabilirlik (beklenen: yanıt, gerçek: bilgi yok (NoValidCitations))  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 2,2 sn · **Model çağrısı:** 2

### HL12 · Normal · ✅ geçti

**Soru:** Cihazımın seri numarası etiketi okunmuyor. Arızası garanti kapsamında onarılır mı?  
**Beklenen:** Hayır. Garanti koşullarına göre seri numarası etiketi sökülmüş veya okunamaz hâle gelmiş cihazlar garanti kapsamı dışındadır.  
**Gerçek:** Maalesef, seri numarası etiketi sökülmüş veya okunamaz hâle gelmiş cihazlar garanti kapsamı dışındadır.  
**Kaynak:** `garanti-kosullari` v1.0 (2025-01-10) › 3. Garanti Kapsamı Dışındaki Durumlar — "Seri numarası etiketi sökülmüş veya okunamaz hâle gelmiş cihazlar" *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ yasak ifade  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,4 sn · **Model çağrısı:** 1

