# Değerlendirme Raporu

- **Tarih:** 2026-10-02 00:27
- **Çalıştırma:** varsayılan yapılandırma
- **Dil modeli:** gemma-4-26b-a4b-it · **Embedding:** bge-m3 · **Arama modu:** hybrid
- **Sonuç:** 16/16 soru geçti

## Özet

| Kategori | Soru | Geçen |
|---|---:|---:|
| Normal | 8 | 8 |
| Cevapsız | 4 | 4 |
| Çelişkili | 4 | 4 |
| **Toplam** | **16** | **16** |

**Arama isabeti** (beklenen kaynak, modele verilen bağlam kadar arama sonucu içinde; 12 soru): yalnız BM25 10/12 · hibrit 12/12  
**Yanıt süresi:** ortalama 2,9 sn, en uzun 27,0 sn

| ID | Kategori | Soru | Beklenen | Sonuç | Süre |
|---|---|---|---|---|---:|
| N01 | Normal | Lumora Termo'yu kurarken hangi Wi-Fi ağını kullanmalıyım? | yanıt | ✅ | 27,0 sn |
| N02 | Normal | Termostatın LED ışığı kırmızı yanıp sönüyor, bu ne anlama geliyor? | yanıt | ✅ | 1,5 sn |
| N03 | Normal | Cihazıma su döküldü ve artık çalışmıyor. Garanti kapsamında ücretsiz onarılır mı? | yanıt | ✅ | 1,5 sn |
| N04 | Normal | Kaç TL ve üzeri siparişlerde kargo ücretsiz oluyor? | yanıt | ✅ | 1,2 sn |
| N05 | Normal | Bir müşteri aynı arızayı üçüncü kez bildirirse talebi nasıl ele almalıyım? | yanıt | ✅ | 1,4 sn |
| N06 | Normal | termostati fabrika ayarlarina nasil donduruyorum | yanıt | ✅ | 1,7 sn |
| N07 | Normal | Sipariş ettiğim termostat ne zaman elime ulaşır ve kurulum için hangi Wi-Fi ağı gerekir? | yanıt | ✅ | 2,0 sn |
| N08 | Normal | Paramı ne zaman geri alırım? | yanıt | ✅ | 1,6 sn |
| U01 | Cevapsız | Lumora'nın genel müdürü kimdir? | bilgi yok | ✅ | 0,0 sn |
| U02 | Cevapsız | Garanti süresini uzatmak için ek garanti paketi satın alabilir miyim? | bilgi yok | ✅ | 1,0 sn |
| U03 | Cevapsız | Lumora Termo'yu Apple HomeKit ile kullanabilir miyim? | bilgi yok | ✅ | 0,9 sn |
| U04 | Cevapsız | Ürünlerinizi yurt dışına gönderiyor musunuz? | bilgi yok | ✅ | 0,0 sn |
| C01 | Çelişkili | Bir ürünü kaç gün içinde iade edebilirim? | yanıt | ✅ | 1,5 sn |
| C02 | Çelişkili | İade ettiğim ürünün parası kaç iş gününde hesabıma geçer? | yanıt | ✅ | 1,4 sn |
| C03 | Çelişkili | Destek ekibine hafta sonu ulaşabilir miyim? | yanıt | ✅ | 2,0 sn |
| C04 | Çelişkili | İade kargo ücretini kim öder? | yanıt | ✅ | 2,6 sn |

## Soru bazında karşılaştırma

### N01 · Normal · ✅ geçti

**Soru:** Lumora Termo'yu kurarken hangi Wi-Fi ağını kullanmalıyım?  
**Beklenen:** Yalnızca 2.4 GHz Wi-Fi ağları desteklenir; cihaz 5 GHz ağlara bağlanamaz.  
**Gerçek:** Lumora Termo kurulumu için yalnızca 2.4 GHz Wi-Fi ağlarını kullanmalısınız; cihaz 5 GHz ağları desteklememektedir.  
**Kaynak:** `kurulum-kilavuzu-lumora-termo` v1.0 (2025-02-01) › 2. Kurulumdan Önce — "Lumora Termo yalnızca 2.4 GHz Wi-Fi ağlarını destekler; 5 GHz ağlara bağlanamaz." *(alıntı doğrulandı)*  
**Kaynak:** `sorun-giderme-baglanti` v1.0 (2025-04-15) › 2. Cihaz Wi-Fi Ağına Bağlanmıyor — "Modeminizin 2.4 GHz yayın yaptığından emin olun; cihaz 5 GHz ağları görmez." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 27,0 sn

### N02 · Normal · ✅ geçti

**Soru:** Termostatın LED ışığı kırmızı yanıp sönüyor, bu ne anlama geliyor?  
**Beklenen:** Kırmızı yanıp sönen LED, cihazın Wi-Fi bağlantısının olmadığını gösterir (sabit kırmızı ise donanım hatasıdır).  
**Gerçek:** Termostatın LED ışığının kırmızı yanıp sönmesi, Wi-Fi bağlantısının olmadığını göstermektedir.  
**Kaynak:** `sorun-giderme-baglanti` v1.0 (2025-04-15) › 1. LED Gösterge Renkleri — "Kırmızı yanıp sönüyor: Wi-Fi bağlantısı yok." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,5 sn

### N03 · Normal · ✅ geçti

**Soru:** Cihazıma su döküldü ve artık çalışmıyor. Garanti kapsamında ücretsiz onarılır mı?  
**Beklenen:** Hayır. Sıvı teması, nem veya su hasarı garanti kapsamı dışındadır.  
**Gerçek:** Maalesef, sıvı teması veya su hasarı gibi durumlar garanti kapsamı dışında yer almaktadır.  
**Kaynak:** `garanti-kosullari` v1.0 (2025-01-10) › 3. Garanti Kapsamı Dışındaki Durumlar — "Sıvı teması, nem veya su hasarı" *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,5 sn

### N04 · Normal · ✅ geçti

**Soru:** Kaç TL ve üzeri siparişlerde kargo ücretsiz oluyor?  
**Beklenen:** 750 TL ve üzerindeki siparişlerde kargo ücretsizdir; altındaki siparişlerde 49,90 TL kargo ücreti alınır.  
**Gerçek:** 750 TL ve üzerindeki siparişlerde kargo ücretsizdir.  
**Kaynak:** `kargo-ve-teslimat` v1.0 (2025-03-01) › 2. Kargo Ücreti — "750 TL ve üzerindeki siparişlerde kargo ücretsizdir." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,2 sn

### N05 · Normal · ✅ geçti

**Soru:** Bir müşteri aynı arızayı üçüncü kez bildirirse talebi nasıl ele almalıyım?  
**Beklenen:** Talep doğrudan L2'ye (Seviye 2) aktarılır ve bir L2 uzmanı vaka sahibi olarak atanır.  
**Gerçek:** Aynı arıza üçüncü kez bildirildiğinde talebi doğrudan L2'ye aktarmalı ve bir L2 uzmanını vaka sahibi olarak atamalısınız.  
**Kaynak:** `sikayet-eskalasyon-proseduru` v1.0 (2025-05-01) › 3. Eskalasyon Kriterleri — "Aynı arıza üçüncü kez bildirildiyse talep doğrudan L2'ye aktarılır ve bir L2 uzmanı vaka sahibi olarak atanır." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,4 sn

### N06 · Normal · ✅ geçti

**Soru:** termostati fabrika ayarlarina nasil donduruyorum  
**Beklenen:** Cihazın sağ yanındaki sıfırlama düğmesine 10 saniye basılı tutun; LED turuncu yanıp sönmeye başlayınca bırakın.  
**Gerçek:** Cihazın sağ yanındaki sıfırlama düğmesine 10 saniye boyunca basılı tutmanız gerekmektedir. LED turuncu yanıp sönmeye başladığında düğmeyi bırakabilirsiniz; bu işlemden sonra cihaz yeniden başlar ve tüm ayarlar silinir.  
**Kaynak:** `kurulum-kilavuzu-lumora-termo` v1.0 (2025-02-01) › 4. Fabrika Ayarlarına Döndürme — "Cihazın sağ yanındaki sıfırlama düğmesine 10 saniye basılı tutun. LED turuncu yanıp sönmeye başladığında düğmeyi bırakın; cihaz yeniden başlar ve tüm ayarlar silinir." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,7 sn

### N07 · Normal · ✅ geçti

**Soru:** Sipariş ettiğim termostat ne zaman elime ulaşır ve kurulum için hangi Wi-Fi ağı gerekir?  
**Beklenen:** Sipariş 1–2 iş günü içinde kargoya verilir ve bölgeye göre 2–4 iş günü içinde teslim edilir; kurulum için 2.4 GHz Wi-Fi ağı gerekir.  
**Gerçek:** Siparişleriniz ödeme onayından sonra 1–2 iş günü içinde kargoya verilir ve bulunduğunuz bölgeye göre 2–4 iş günü içinde teslim edilir. Kurulum için yalnızca 2.4 GHz Wi-Fi ağı gereklidir; cihaz 5 GHz ağları desteklemez.  
**Kaynak:** `kargo-ve-teslimat` v1.0 (2025-03-01) › 1. Teslimat Süresi — "Siparişler, ödeme onayından sonra 1–2 iş günü içinde kargoya verilir. Kargoya verilen siparişler bulunduğunuz bölgeye göre 2–4 iş günü içinde teslim edilir." *(alıntı doğrulandı)*  
**Kaynak:** `kurulum-kilavuzu-lumora-termo` v1.0 (2025-02-01) › 2. Kurulumdan Önce — "Lumora Termo yalnızca 2.4 GHz Wi-Fi ağlarını destekler; 5 GHz ağlara bağlanamaz." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynaklar · ✅ içerik · ✅ içerik  
**Arama isabeti:** BM25 ❌ · hibrit ✅ · **Süre:** 2,0 sn

### N08 · Normal · ✅ geçti

**Soru:** Paramı ne zaman geri alırım?  
**Beklenen:** İade edilen ürün depoya ulaşıp kontrol edildikten sonra ücret 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir (v2.0). Soruda 'iade' kelimesi geçmiyor; bölümü anlamsal arama bulmalı.  
**Gerçek:** İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 6. Para İadesi — "İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ içerik · ✅ yasak ifade  
**Arama isabeti:** BM25 ❌ · hibrit ✅ · **Süre:** 1,6 sn

### U01 · Cevapsız · ✅ geçti

**Soru:** Lumora'nın genel müdürü kimdir?  
**Beklenen:** Dokümanlarda bu bilgi yok; sistem yanıt üretmeyip bilgi olmadığını belirtmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: LowRelevance)*  
**Kontroller:** ✅ yanıtlanabilirlik  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,0 sn

### U02 · Cevapsız · ✅ geçti

**Soru:** Garanti süresini uzatmak için ek garanti paketi satın alabilir miyim?  
**Beklenen:** Dokümanlarda garanti uzatma paketi bilgisi yok (yalnızca 2 yıllık standart garanti anlatılıyor); bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Verilen kaynaklarda ek garanti paketi satın alınıp alınamayacağına dair bir bilgi bulunmamaktadır.  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 1,0 sn

### U03 · Cevapsız · ✅ geçti

**Soru:** Lumora Termo'yu Apple HomeKit ile kullanabilir miyim?  
**Beklenen:** Dokümanlarda HomeKit uyumluluğundan bahsedilmiyor; bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Lumora Termo'nun Apple HomeKit desteği hakkında kaynaklarda bilgi bulunmamaktadır.  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,9 sn

### U04 · Cevapsız · ✅ geçti

**Soru:** Ürünlerinizi yurt dışına gönderiyor musunuz?  
**Beklenen:** Dokümanlarda yurt dışı gönderimden bahsedilmiyor; bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: LowRelevance)*  
**Kontroller:** ✅ yanıtlanabilirlik  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,0 sn

### C01 · Çelişkili · ✅ geçti

**Soru:** Bir ürünü kaç gün içinde iade edebilirim?  
**Beklenen:** Teslimattan itibaren 30 gün (İade Politikası v2.0, 2025-06-01). v1.0'daki 14 günlük kural geçersiz.  
**Gerçek:** Ürünü, teslim aldığınız tarihten itibaren 30 gün içinde iade edebilirsiniz. Bu süre, kargo firmasının teslimat kaydındaki tarih esas alınarak hesaplanmaktadır.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 2. İade Süresi — "Müşteriler, ürünü teslim aldıkları tarihten itibaren 30 gün içinde iade talebinde bulunabilir. Süre, kargo firmasının teslimat kaydındaki tarih esas alınarak hesaplanır." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ içerik · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,5 sn

### C02 · Çelişkili · ✅ geçti

**Soru:** İade ettiğim ürünün parası kaç iş gününde hesabıma geçer?  
**Beklenen:** Ürün depoya ulaşıp kontrol edildikten sonra 5 iş günü içinde iade edilir (v2.0). v1.0'daki 10 iş günü geçersiz.  
**Gerçek:** İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 6. Para İadesi — "İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ içerik · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,4 sn

### C03 · Çelişkili · ✅ geçti

**Soru:** Destek ekibine hafta sonu ulaşabilir miyim?  
**Beklenen:** Evet. Canlı sohbet 7/24, çağrı merkezi hafta sonu dahil her gün 08:00–22:00 hizmet verir (v2.0). v1.0'daki 'hafta sonu destek verilmez' kuralı geçersiz.  
**Gerçek:** Evet, Lumora mobil uygulaması ve web sitesindeki canlı sohbet kanalı haftanın 7 günü, 24 saat hizmet vermektedir. Ayrıca, 0850 000 00 00 numaralı çağrı merkezimize hafta sonları dahil her gün 08:00–22:00 saatleri arasında ulaşabilirsiniz.  
**Kaynak:** `destek-kanallari-v2` v2.0 (2025-09-01) › 1. Canlı Sohbet — "Lumora mobil uygulaması ve web sitesindeki canlı sohbet kanalı haftanın 7 günü, 24 saat hizmet verir." *(alıntı doğrulandı)*  
**Kaynak:** `destek-kanallari-v2` v2.0 (2025-09-01) › 2. Telefon Desteği — "Çağrı merkezimize 0850 000 00 00 numarasından hafta sonu ve resmî tatiller dahil her gün 08:00–22:00 saatleri arasında ulaşabilirsiniz." *(alıntı doğrulandı)*  
**Sürüm kararı:** `destek-kanallari-v1` v1.0 elendi — 2.0 sürümü (2025-09-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ içerik · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 2,0 sn

### C04 · Çelişkili · ✅ geçti

**Soru:** İade kargo ücretini kim öder?  
**Beklenen:** Anlaşmalı kargo ile iade kodu kullanılarak gönderilen iadelerde kargo ücretsizdir; ücreti Lumora karşılar (İade Politikası v2.0). 2024 tarihli SSS'deki 'müşteri öder' bilgisi ve v1.0 geçersiz.  
**Gerçek:** İade kodunu kullanarak anlaşmalı kargo firmamızla gönderdiğiniz iadelerin kargo ücretini Lumora karşılamaktadır.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 5. İade Kargo Ücreti — "İade kargosu ücretsizdir: iade kodunu kullanarak anlaşmalı kargo firmamızla gönderdiğiniz iadelerin kargo ücretini Lumora karşılar." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kaynaklar arası çelişki:** İade kargo ücreti — seçilen `iade-politikasi-v2`, elenen `sss-genel`; kurala uygun: evet  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ içerik · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 2,6 sn

