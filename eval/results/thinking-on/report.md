# Değerlendirme Raporu

- **Tarih:** 2026-10-02 13:46
- **Çalıştırma:** thinking-on
- **Dil modeli:** gemma-4-26b-a4b-it · **Embedding:** bge-m3 · **Arama modu:** hybrid
- **Sonuç:** 29/30 soru geçti

## Özet

| Kategori | Soru | Geçen |
|---|---:|---:|
| Normal | 14 | 14 |
| Cevapsız | 8 | 8 |
| Çelişkili | 8 | 7 |
| **Toplam** | **30** | **29** |

**Arama isabeti** (beklenen kaynak, sunucunun varsayılan topK değeri kadar arama sonucu içinde — sürüm çözümünden önce; 22 soru): yalnız BM25 20/22 · hibrit 22/22  
**Yanıt süresi:** medyan 8,8 sn, ortalama 11,4 sn, en uzun 47,9 sn (Kapı 1'de reddedilen sorular modele gitmediği için ~0 sn)  
**Düzeltme turu:** 0 soruda model ikinci kez çağrıldı  
**Halüsinasyon sinyali:** 0/21 yanıtta (desteksiz iddia oranı; ayrıntı aşağıda)

| ID | Kategori | Soru | Beklenen | Sonuç | Süre |
|---|---|---|---|---|---:|
| N01 | Normal | Lumora Termo'yu kurarken hangi Wi-Fi ağını kullanmalıyım? | yanıt | ✅ | 11,1 sn |
| N02 | Normal | Termostatın LED ışığı kırmızı yanıp sönüyor, bu ne anlama geliyor? | yanıt | ✅ | 16,4 sn |
| N03 | Normal | Cihazıma su döküldü ve artık çalışmıyor. Garanti kapsamında ücretsiz onarılır mı? | yanıt | ✅ | 23,1 sn |
| N04 | Normal | Kaç TL ve üzeri siparişlerde kargo ücretsiz oluyor? | yanıt | ✅ | 19,0 sn |
| N05 | Normal | Bir müşteri aynı arızayı üçüncü kez bildirirse talebi nasıl ele almalıyım? | yanıt | ✅ | 30,8 sn |
| N06 | Normal | termostati fabrika ayarlarina nasil donduruyorum | yanıt | ✅ | 23,4 sn |
| N07 | Normal | Sipariş ettiğim termostat ne zaman elime ulaşır ve kurulum için hangi Wi-Fi ağı gerekir? | yanıt | ✅ | 10,8 sn |
| N08 | Normal | Paramı ne zaman geri alırım? | yanıt | ✅ | 10,9 sn |
| N09 | Normal | Garanti başvurusunu nereden yaparım ve hangi bilgiler gerekir? | yanıt | ✅ | 10,8 sn |
| N10 | Normal | Garanti kapsamındaki cihazımın onarımı en geç ne kadar sürer? | yanıt | ✅ | 6,4 sn |
| N11 | Normal | Uygulamada E02 hata kodu görüyorum, sorun ne ve ne yapmalıyım? | yanıt | ✅ | 8,8 sn |
| N12 | Normal | Siparişim hasarlı geldi, ne yapmalıyım? | yanıt | ✅ | 7,1 sn |
| N13 | Normal | Müşteri termostattan yanık kokusu geldiğini söylüyor. Talebi nasıl yönetmeliyim? | yanıt | ✅ | 8,0 sn |
| N14 | Normal | Kombiden gelen kabloları termostatta nereye bağlamalıyım? | yanıt | ✅ | 8,0 sn |
| U01 | Cevapsız | Lumora'nın genel müdürü kimdir? | bilgi yok | ✅ | 0,0 sn |
| U02 | Cevapsız | Garanti süresini uzatmak için ek garanti paketi satın alabilir miyim? | bilgi yok | ✅ | 4,5 sn |
| U03 | Cevapsız | Lumora Termo'yu Apple HomeKit ile kullanabilir miyim? | bilgi yok | ✅ | 7,0 sn |
| U04 | Cevapsız | Ürünlerinizi yurt dışına gönderiyor musunuz? | bilgi yok | ✅ | 0,0 sn |
| U05 | Cevapsız | Lumora Termo'nun fiyatı ne kadar? | bilgi yok | ✅ | 4,0 sn |
| U06 | Cevapsız | Verdiğim siparişi nasıl iptal edebilirim? | bilgi yok | ✅ | 6,4 sn |
| U07 | Cevapsız | İndirim kodumu siparişte nereye girmeliyim? | bilgi yok | ✅ | 3,9 sn |
| U08 | Cevapsız | Lumora Termo WPA3 şifrelemeli Wi-Fi ağlarını destekliyor mu? | bilgi yok | ✅ | 5,6 sn |
| C01 | Çelişkili | Bir ürünü kaç gün içinde iade edebilirim? | yanıt | ❌ | 47,9 sn |
| C02 | Çelişkili | İade ettiğim ürünün parası kaç iş gününde hesabıma geçer? | yanıt | ✅ | 8,8 sn |
| C03 | Çelişkili | Destek ekibine hafta sonu ulaşabilir miyim? | yanıt | ✅ | 13,1 sn |
| C04 | Çelişkili | İade kargo ücretini kim öder? | yanıt | ✅ | 11,1 sn |
| C05 | Çelişkili | Çağrı merkezinize hangi saatlerde ulaşabilirim? | yanıt | ✅ | 9,5 sn |
| C06 | Çelişkili | Canlı sohbetten destek alabilir miyim? | yanıt | ✅ | 10,5 sn |
| C07 | Çelişkili | İade talebi oluşturduktan sonra iade kodum ne zaman gelir? | yanıt | ✅ | 8,0 sn |
| C08 | Çelişkili | Termostatı bir pazaryerinden satın aldım. İadeyi nasıl yapacağım? | yanıt | ✅ | 8,1 sn |

## Halüsinasyon sinyalleri

Deterministik vekil ölçülerdir, kanıt değildir: cevapsız soruya yanıt, atıf yapılan dokümanlarda geçmeyen sayı, kaynakta birebir bulunamayan alıntı ve yasak ifade (ters karar, eski kural ya da bilgi tabanında olmayan genel bilgi). Sayı içermeyen ve yasak listesinde olmayan bir uydurma yakalanmaz; yanıtlar ayrıca elle okunur.

Hiçbir yanıtta sinyal yok.

## Soru bazında karşılaştırma

### N01 · Normal · ✅ geçti

**Soru:** Lumora Termo'yu kurarken hangi Wi-Fi ağını kullanmalıyım?  
**Beklenen:** Yalnızca 2.4 GHz Wi-Fi ağları desteklenir; cihaz 5 GHz ağlara bağlanamaz.  
**Gerçek:** Lumora Termo kurulumu için 2.4 GHz Wi-Fi ağınızı kullanmalısınız; cihaz 5 GHz ağları desteklemez.  
**Kaynak:** `kurulum-kilavuzu-lumora-termo` v1.0 (2025-02-01) › 3. Adım Adım Kurulum — "2.4 GHz Wi-Fi ağınızı seçip şifrenizi girin." *(alıntı doğrulandı)*  
**Kaynak:** `kurulum-kilavuzu-lumora-termo` v1.0 (2025-02-01) › 2. Kurulumdan Önce — "Lumora Termo yalnızca 2.4 GHz Wi-Fi ağlarını destekler; 5 GHz ağlara bağlanamaz." *(alıntı doğrulandı)*  
**Kaynak:** `sorun-giderme-baglanti` v1.0 (2025-04-15) › 2. Cihaz Wi-Fi Ağına Bağlanmıyor — "Modeminizin 2.4 GHz yayın yaptığından emin olun; cihaz 5 GHz ağları görmez." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 11,1 sn · **Model çağrısı:** 1

### N02 · Normal · ✅ geçti

**Soru:** Termostatın LED ışığı kırmızı yanıp sönüyor, bu ne anlama geliyor?  
**Beklenen:** Kırmızı yanıp sönen LED, cihazın Wi-Fi bağlantısının olmadığını gösterir (sabit kırmızı ise donanım hatasıdır).  
**Gerçek:** Cihazın Wi-Fi bağlantısı yok.  
**Kaynak:** `sorun-giderme-baglanti` v1.0 (2025-04-15) › 1. LED Gösterge Renkleri — "Kırmızı yanıp sönüyor: Wi-Fi bağlantısı yok." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 16,4 sn · **Model çağrısı:** 1

### N03 · Normal · ✅ geçti

**Soru:** Cihazıma su döküldü ve artık çalışmıyor. Garanti kapsamında ücretsiz onarılır mı?  
**Beklenen:** Hayır. Sıvı teması, nem veya su hasarı garanti kapsamı dışındadır.  
**Gerçek:** Su teması, nem veya su hasarı gibi durumlar garanti kapsamı dışındadır.  
**Kaynak:** `garanti-kosullari` v1.0 (2025-01-10) › 3. Garanti Kapsamı Dışındaki Durumlar — "Sıvı teması, nem veya su hasarı" *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 23,1 sn · **Model çağrısı:** 1

### N04 · Normal · ✅ geçti

**Soru:** Kaç TL ve üzeri siparişlerde kargo ücretsiz oluyor?  
**Beklenen:** 750 TL ve üzerindeki siparişlerde kargo ücretsizdir; altındaki siparişlerde 49,90 TL kargo ücreti alınır.  
**Gerçek:** 750 TL ve üzerindeki siparişlerde kargo ücretsizdir.  
**Kaynak:** `kargo-ve-teslimat` v1.0 (2025-03-01) › 2. Kargo Ücreti — "750 TL ve üzerindeki siparişlerde kargo ücretsizdir." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ içerik · ✅ koşul · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 19,0 sn · **Model çağrısı:** 1

### N05 · Normal · ✅ geçti

**Soru:** Bir müşteri aynı arızayı üçüncü kez bildirirse talebi nasıl ele almalıyım?  
**Beklenen:** Talep doğrudan L2'ye (Seviye 2) aktarılır ve bir L2 uzmanı vaka sahibi olarak atanır.  
**Gerçek:** Talebi doğrudan L2'ye aktarmalı ve bir L2 uzmanının vaka sahibi olarak atanmasını sağlamalısınız.  
**Eksik bilgi (model):** null  
**Kaynak:** `sikayet-eskalasyon-proseduru` v1.0 (2025-05-01) › 3. Eskalasyon Kriterleri — "Aynı arıza üçüncü kez bildirildiyse talep doğrudan L2'ye aktarılır ve bir L2 uzmanı vaka sahibi olarak atanır." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 30,8 sn · **Model çağrısı:** 1

### N06 · Normal · ✅ geçti

**Soru:** termostati fabrika ayarlarina nasil donduruyorum  
**Beklenen:** Cihazın sağ yanındaki sıfırlama düğmesine 10 saniye basılı tutun; LED turuncu yanıp sönmeye başlayınca bırakın.  
**Gerçek:** Cihazın sağ yanındaki sıfırlama düğmesine 10 saniye boyunca basılı tutun. LED turuncu yanıp sönmeye başladığında düğmeyi bırakın; cihaz yeniden başlayacak ve tüm ayarlar silinecektir.  
**Kaynak:** `kurulum-kilavuzu-lumora-termo` v1.0 (2025-02-01) › 4. Fabrika Ayarlarına Döndürme — "Cihazın sağ yanındaki sıfırlama düğmesine 10 saniye basılı tutun. LED turuncu yanıp sönmeye başladığında düğmeyi bırakın; cihaz yeniden başlar ve tüm ayarlar silinir." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 23,4 sn · **Model çağrısı:** 1

### N07 · Normal · ✅ geçti

**Soru:** Sipariş ettiğim termostat ne zaman elime ulaşır ve kurulum için hangi Wi-Fi ağı gerekir?  
**Beklenen:** Sipariş 1–2 iş günü içinde kargoya verilir ve bölgeye göre 2–4 iş günü içinde teslim edilir; kurulum için 2.4 GHz Wi-Fi ağı gerekir.  
**Gerçek:** Siparişleriniz ödeme onayından sonra 1–2 iş günü içinde kargoya verilir ve bulunduğunuz bölgeye göre 2–4 iş günü içinde teslim edilir. Kurulum için yalnızca 2.4 GHz Wi-Fi ağı gereklidir; cihaz 5 GHz ağları desteklemez.  
**Kaynak:** `kargo-ve-teslimat` v1.0 (2025-03-01) › 1. Teslimat Süresi — "Siparişler, ödeme onayından sonra 1–2 iş günü içinde kargoya verilir. Kargoya verilen siparişler bulunduğunuz bölgeye göre 2–4 iş günü içinde teslim edilir." *(alıntı doğrulandı)*  
**Kaynak:** `kurulum-kilavuzu-lumora-termo` v1.0 (2025-02-01) › 2. Kurulumdan Önce — "Lumora Termo yalnızca 2.4 GHz Wi-Fi ağlarını destekler; 5 GHz ağlara bağlanamaz." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynaklar · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ içerik  
**Arama isabeti:** BM25 ❌ · hibrit ✅ · **Süre:** 10,8 sn · **Model çağrısı:** 1

### N08 · Normal · ✅ geçti

**Soru:** Paramı ne zaman geri alırım?  
**Beklenen:** İade edilen ürün depoya ulaşıp kontrol edildikten sonra ücret 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir (v2.0). Soruda 'iade' kelimesi geçmiyor; bölümü anlamsal arama bulmalı.  
**Gerçek:** İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 6. Para İadesi — "İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ koşul · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade  
**Arama isabeti:** BM25 ❌ · hibrit ✅ · **Süre:** 10,9 sn · **Model çağrısı:** 1

### N09 · Normal · ✅ geçti

**Soru:** Garanti başvurusunu nereden yaparım ve hangi bilgiler gerekir?  
**Beklenen:** Lumora uygulamasında Destek > Garanti Talebi menüsünden; başvuru için cihazın seri numarası ve fatura gerekir (seri numarası cihazın arka yüzünde ve uygulamadaki Cihaz Bilgileri ekranında yer alır).  
**Gerçek:** Garanti başvurusu Lumora uygulamasında Destek > Garanti Talebi menüsünden yapılabilir. Başvuru için cihazın seri numarası ve fatura gereklidir. Seri numarası cihazın arka yüzünde veya uygulamadaki Cihaz Bilgileri ekranında yer alır.  
**Kaynak:** `garanti-kosullari` v1.0 (2025-01-10) › 4. Garanti Başvurusu — "Garanti başvurusu Lumora uygulamasında Destek > Garanti Talebi menüsünden yapılır." *(alıntı doğrulandı)*  
**Kaynak:** `garanti-kosullari` v1.0 (2025-01-10) › 4. Garanti Başvurusu — "Başvuru için cihazın seri numarası ve fatura gerekir." *(alıntı doğrulandı)*  
**Kaynak:** `garanti-kosullari` v1.0 (2025-01-10) › 4. Garanti Başvurusu — "Seri numarası cihazın arka yüzünde ve uygulamadaki Cihaz Bilgileri ekranında yer alır." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ içerik · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 10,8 sn · **Model çağrısı:** 1

### N10 · Normal · ✅ geçti

**Soru:** Garanti kapsamındaki cihazımın onarımı en geç ne kadar sürer?  
**Beklenen:** Cihazın servise ulaşmasından itibaren en geç 20 iş günü; bu süre aşılırsa cihazın yenisiyle değiştirilmesi talep edilebilir.  
**Gerçek:** Garanti kapsamındaki onarımlar, cihazın servise ulaşmasından itibaren en geç 20 iş günü içinde tamamlanır.  
**Kaynak:** `garanti-kosullari` v1.0 (2025-01-10) › 5. Onarım Süresi — "Garanti kapsamındaki onarımlar, cihazın servise ulaşmasından itibaren en geç 20 iş günü içinde tamamlanır." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ koşul  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 6,4 sn · **Model çağrısı:** 1

### N11 · Normal · ✅ geçti

**Soru:** Uygulamada E02 hata kodu görüyorum, sorun ne ve ne yapmalıyım?  
**Beklenen:** E02, Lumora sunucularına ulaşılamadığını gösterir; internet bağlantınızı kontrol edin.  
**Gerçek:** Lumora sunucularına ulaşılamıyor, lütfen internet bağlantınızı kontrol edin.  
**Kaynak:** `sorun-giderme-baglanti` v1.0 (2025-04-15) › 3. Hata Kodları — "E02: Lumora sunucularına ulaşılamıyor. İnternet bağlantınızı kontrol edin." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 8,8 sn · **Model çağrısı:** 1

### N12 · Normal · ✅ geçti

**Soru:** Siparişim hasarlı geldi, ne yapmalıyım?  
**Beklenen:** Teslimattan itibaren 3 gün içinde fotoğraflarla birlikte uygulamadaki Destek > Teslimat Sorunu menüsünden bildirin; ürün ücretsiz değiştirilir. Paket hasarlı görünüyorsa teslim alırken kargo görevlisine hasar tespit tutanağı tutturulmalıdır.  
**Gerçek:** Paket hasarlı görünüyorsa teslim alırken kargo görevlisine hasar tespit tutanağı tutturmalısınız. Hasarlı ürünü, teslimattan itibaren 3 gün içinde fotoğraflarla birlikte uygulamadaki Destek > Teslimat Sorunu menüsünden bildirmeniz durumunda ürün ücretsiz olarak değiştirilir.  
**Kaynak:** `kargo-ve-teslimat` v1.0 (2025-03-01) › 4. Hasarlı veya Eksik Teslimat — "Paket hasarlı görünüyorsa teslim alırken kargo görevlisine hasar tespit tutanağı tutturun. Hasarlı veya eksik ürünü, teslimattan itibaren 3 gün içinde fotoğraflarla birlikte uygulamadaki Destek > Teslimat Sorunu menüsünden bildirin; ürün ücretsiz olarak değiştirilir." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ içerik · ✅ koşul  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 7,1 sn · **Model çağrısı:** 1

### N13 · Normal · ✅ geçti

**Soru:** Müşteri termostattan yanık kokusu geldiğini söylüyor. Talebi nasıl yönetmeliyim?  
**Beklenen:** Müşteriden cihazın elektriğini hemen kesmesi istenir ve talep doğrudan L3'e (ürün mühendisliği) aktarılır.  
**Gerçek:** Müşteriden cihazın elektriğini hemen kesmesini istemeli ve talebi doğrudan L3'e aktarmalısınız.  
**Kaynak:** `sikayet-eskalasyon-proseduru` v1.0 (2025-05-01) › 3. Eskalasyon Kriterleri — "müşteriden cihazın elektriğini hemen kesmesi istenir ve talep doğrudan L3'e aktarılır" *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 8,0 sn · **Model çağrısı:** 1

### N14 · Normal · ✅ geçti

**Soru:** Kombiden gelen kabloları termostatta nereye bağlamalıyım?  
**Beklenen:** Montaj plakasındaki COM ve NO uçlarına (kurulumdan önce kombinin elektrik bağlantısı kesilmelidir).  
**Gerçek:** Kombiden gelen kabloları montaj plakasındaki COM ve NO uçlarına bağlamalısınız.  
**Kaynak:** `kurulum-kilavuzu-lumora-termo` v1.0 (2025-02-01) › 3. Adım Adım Kurulum — "Kombiden gelen kabloları plakadaki COM ve NO uçlarına bağlayın." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 8,0 sn · **Model çağrısı:** 1

### U01 · Cevapsız · ✅ geçti

**Soru:** Lumora'nın genel müdürü kimdir?  
**Beklenen:** Dokümanlarda bu bilgi yok; sistem yanıt üretmeyip bilgi olmadığını belirtmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: LowRelevance)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,0 sn · **Model çağrısı:** 0

### U02 · Cevapsız · ✅ geçti

**Soru:** Garanti süresini uzatmak için ek garanti paketi satın alabilir miyim?  
**Beklenen:** Dokümanlarda garanti uzatma paketi bilgisi yok (yalnızca 2 yıllık standart garanti anlatılıyor); bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Ek garanti paketi satın alınıp alınamayacağı hakkında bilgi bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 4,5 sn · **Model çağrısı:** 1

### U03 · Cevapsız · ✅ geçti

**Soru:** Lumora Termo'yu Apple HomeKit ile kullanabilir miyim?  
**Beklenen:** Dokümanlarda HomeKit uyumluluğundan bahsedilmiyor; bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Apple HomeKit uyumluluğu hakkında bilgi bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 7,0 sn · **Model çağrısı:** 1

### U04 · Cevapsız · ✅ geçti

**Soru:** Ürünlerinizi yurt dışına gönderiyor musunuz?  
**Beklenen:** Dokümanlarda yurt dışı gönderimden bahsedilmiyor; bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: LowRelevance)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,0 sn · **Model çağrısı:** 0

### U05 · Cevapsız · ✅ geçti

**Soru:** Lumora Termo'nun fiyatı ne kadar?  
**Beklenen:** Dokümanlarda ürün fiyatı yok; bilgi olmadığı belirtilmeli. 750 TL kargo eşiği ve 49,90 TL kargo ücreti fiyat değildir.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Lumora Termo'nun fiyatı hakkında bilgi kaynaklarda bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 4,0 sn · **Model çağrısı:** 1

### U06 · Cevapsız · ✅ geçti

**Soru:** Verdiğim siparişi nasıl iptal edebilirim?  
**Beklenen:** Dokümanlarda sipariş iptali anlatılmıyor (yalnızca kargoya verilmeden önce adres değişikliği ve teslimattan sonra iade var); bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Sipariş iptali süreciyle ilgili bilgi kaynaklarda bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 6,4 sn · **Model çağrısı:** 1

### U07 · Cevapsız · ✅ geçti

**Soru:** İndirim kodumu siparişte nereye girmeliyim?  
**Beklenen:** Dokümanlarda indirim kodu ya da kampanya bilgisi yok; bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** İndirim kodunun sipariş sırasında nereye girileceğine dair bilgi kaynaklarda bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 3,9 sn · **Model çağrısı:** 1

### U08 · Cevapsız · ✅ geçti

**Soru:** Lumora Termo WPA3 şifrelemeli Wi-Fi ağlarını destekliyor mu?  
**Beklenen:** Dokümanlarda Wi-Fi şifreleme standardı geçmiyor (yalnızca 2.4 GHz desteği anlatılıyor); bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** WPA3 şifreleme standardı desteği hakkında bilgi bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 5,6 sn · **Model çağrısı:** 1

### C01 · Çelişkili · ❌ kaldı

**Soru:** Bir ürünü kaç gün içinde iade edebilirim?  
**Beklenen:** Teslimattan itibaren 30 gün (İade Politikası v2.0, 2025-06-01). v1.0'daki 14 günlük kural geçersiz.  
**Gerçek:** HTTP 502 — Dil modeli geçerli bir yanıt üretemedi. Lütfen tekrar deneyin.  
**Kontroller:** ❌ yanıt (HTTP 502: Dil modeli geçerli bir yanıt üretemedi. Lütfen tekrar deneyin.)  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 47,9 sn

### C02 · Çelişkili · ✅ geçti

**Soru:** İade ettiğim ürünün parası kaç iş gününde hesabıma geçer?  
**Beklenen:** Ürün depoya ulaşıp kontrol edildikten sonra 5 iş günü içinde iade edilir (v2.0). v1.0'daki 10 iş günü geçersiz.  
**Gerçek:** İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 6. Para İadesi — "İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ koşul · ✅ yasak ifade · ✅ yasak ifade · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 8,8 sn · **Model çağrısı:** 1

### C03 · Çelişkili · ✅ geçti

**Soru:** Destek ekibine hafta sonu ulaşabilir miyim?  
**Beklenen:** Evet. Canlı sohbet 7/24, çağrı merkezi hafta sonu dahil her gün 08:00–22:00 hizmet verir (v2.0). v1.0'daki 'hafta sonu destek verilmez' kuralı geçersiz.  
**Gerçek:** Evet, hafta sonu destek ekibine ulaşabilirsiniz. Telefonla 08:00–22:00 saatleri arasında, canlı sohbet ile haftanın 7 günü 24 saat boyunca, e-posta yoluyla ise 24 saat içinde yanıt alacak şekilde iletişime geçebilirsiniz.  
**Kaynak:** `destek-kanallari-v2` v2.0 (2025-09-01) › 2. Telefon Desteği — "Çağrı merkezimize 0850 000 00 00 numarasından hafta sonu ve resmî tatiller dahil her gün 08:00–22:00 saatleri arasında ulaşabilirsiniz." *(alıntı doğrulandı)*  
**Kaynak:** `destek-kanallari-v2` v2.0 (2025-09-01) › 1. Canlı Sohbet — "Lumora mobil uygulaması ve web sitesindeki canlı sohbet kanalı haftanın 7 günü, 24 saat hizmet verir." *(alıntı doğrulandı)*  
**Kaynak:** `destek-kanallari-v2` v2.0 (2025-09-01) › 3. E-posta Desteği — "destek@lumora.example adresine gönderilen e-postalara 24 saat içinde ilk yanıt verilir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `destek-kanallari-v1` v1.0 elendi — 2.0 sürümü (2025-09-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 13,1 sn · **Model çağrısı:** 1

### C04 · Çelişkili · ✅ geçti

**Soru:** İade kargo ücretini kim öder?  
**Beklenen:** Anlaşmalı kargo ile iade kodu kullanılarak gönderilen iadelerde kargo ücretsizdir; ücreti Lumora karşılar (İade Politikası v2.0). 2024 tarihli SSS'deki 'müşteri öder' bilgisi ve v1.0 geçersiz.  
**Gerçek:** İade kodunu kullanarak anlaşmalı kargo firmamızla gönderdiğiniz iadelerin kargo ücretini Lumora karşılar.  
**Eksik bilgi (model):** null  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 5. İade Kargo Ücreti — "İade kargosu ücretsizdir: iade kodunu kullanarak anlaşmalı kargo firmamızla gönderdiğiniz iadelerin kargo ücretini Lumora karşılar." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kaynaklar arası çelişki:** İade kargo ücreti — seçilen `iade-politikasi-v2`, elenen `sss-genel`; kurala uygun: evet  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ yasak ifade · ✅ eski sürüm elendi · ✅ çelişki kaydı  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 11,1 sn · **Model çağrısı:** 1

### C05 · Çelişkili · ✅ geçti

**Soru:** Çağrı merkezinize hangi saatlerde ulaşabilirim?  
**Beklenen:** Hafta sonu ve resmî tatiller dahil her gün 08:00–22:00 (0850 000 00 00; Destek Kanalları v2.0). v1.0'daki 'hafta içi 09:00–18:00' bilgisi geçersiz.  
**Gerçek:** Çağrı merkezimize hafta sonu ve resmî tatiller dahil her gün 08:00–22:00 saatleri arasında ulaşabilirsiniz.  
**Kaynak:** `destek-kanallari-v2` v2.0 (2025-09-01) › 2. Telefon Desteği — "hafta sonu ve resmî tatiller dahil her gün 08:00–22:00 saatleri arasında ulaşabilirsiniz." *(alıntı doğrulandı)*  
**Sürüm kararı:** `destek-kanallari-v1` v1.0 elendi — 2.0 sürümü (2025-09-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ içerik · ✅ yasak ifade · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 9,5 sn · **Model çağrısı:** 1

### C06 · Çelişkili · ✅ geçti

**Soru:** Canlı sohbetten destek alabilir miyim?  
**Beklenen:** Evet. Mobil uygulama ve web sitesindeki canlı sohbet haftanın 7 günü 24 saat hizmet verir; ilk yanıt süresi hedefi 5 dakikadır (v2.0). v1.0'daki 'canlı sohbet sunulmamaktadır' bilgisi geçersiz.  
**Gerçek:** Evet, Lumora mobil uygulaması ve web sitesindeki canlı sohbet kanalı haftanın 7 günü, 24 saat hizmet vermektedir.  
**Kaynak:** `destek-kanallari-v2` v2.0 (2025-09-01) › 1. Canlı Sohbet — "Lumora mobil uygulaması ve web sitesindeki canlı sohbet kanalı haftanın 7 günü, 24 saat hizmet verir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `destek-kanallari-v1` v1.0 elendi — 2.0 sürümü (2025-09-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ yasak ifade · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 10,5 sn · **Model çağrısı:** 1

### C07 · Çelişkili · ✅ geçti

**Soru:** İade talebi oluşturduktan sonra iade kodum ne zaman gelir?  
**Beklenen:** İade kodu talep anında oluşturulur ve Lumora uygulamasında görüntülenir (v2.0). v1.0'daki 'en geç 2 iş günü içinde e-posta ile' bilgisi geçersiz.  
**Gerçek:** İade kodu, talep oluşturulduğu anda oluşturulur ve Lumora uygulamasında görüntülenir.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 4. İade Talebi Nasıl Oluşturulur? — "İade kodu, talep anında oluşturulur ve uygulamada görüntülenir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 8,0 sn · **Model çağrısı:** 1

### C08 · Çelişkili · ✅ geçti

**Soru:** Termostatı bir pazaryerinden satın aldım. İadeyi nasıl yapacağım?  
**Beklenen:** Bayi ve pazaryeri satışlarında, satın alma yapılan satıcının iade koşulları geçerlidir. Bu bölüm v1.0 ve v2.0'da aynıdır; yine de güncel sürüm gösterilmeli ve v1.0 elenmelidir.  
**Gerçek:** Pazaryeri üzerinden yapılan satın alımlarda, satın alma yaptığınız satıcının iade koşulları geçerlidir.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 1. Kapsam — "Bayi ve pazaryeri satışlarında, satın alma yapılan satıcının iade koşulları geçerlidir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 8,1 sn · **Model çağrısı:** 1

