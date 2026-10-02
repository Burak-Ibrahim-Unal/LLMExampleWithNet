# Değerlendirme Raporu

- **Tarih:** 2026-10-02 13:37
- **Çalıştırma:** holdout-2
- **Dil modeli:** gemma-4-26b-a4b-it · **Embedding:** bge-m3 · **Arama modu:** hybrid
- **Sonuç:** 12/12 soru geçti

## Özet

| Kategori | Soru | Geçen |
|---|---:|---:|
| Normal | 6 | 6 |
| Cevapsız | 3 | 3 |
| Çelişkili | 3 | 3 |
| **Toplam** | **12** | **12** |

**Arama isabeti** (beklenen kaynak, sunucunun varsayılan topK değeri kadar arama sonucu içinde — sürüm çözümünden önce; 9 soru): yalnız BM25 9/9 · hibrit 9/9  
**Yanıt süresi:** medyan 1,5 sn, ortalama 1,6 sn, en uzun 4,1 sn (Kapı 1'de reddedilen sorular modele gitmediği için ~0 sn)  
**Düzeltme turu:** 0 soruda model ikinci kez çağrıldı  
**Halüsinasyon sinyali:** 0/9 yanıtta (desteksiz iddia oranı; ayrıntı aşağıda)

| ID | Kategori | Soru | Beklenen | Sonuç | Süre |
|---|---|---|---|---|---:|
| H13 | Normal | Şirketim adına fatura kestirebilir miyim, hangi bilgileri vermem gerekiyor? | yanıt | ✅ | 4,1 sn |
| H14 | Normal | Termostatın yazılımı ne zaman güncelleniyor, güncelleme sırasında nelere dikkat etmeliyim? | yanıt | ✅ | 1,8 sn |
| H15 | Normal | Kombim tamamen çalışmıyor çünkü termostat kombiyi çalıştırmıyor. Destek ekibi ne kadar sürede dönüş yapar? | yanıt | ✅ | 1,6 sn |
| H16 | Normal | Lumora Termo'nun kutusundan hangi parçalar çıkıyor? | yanıt | ✅ | 1,5 sn |
| H17 | Normal | Kredi kartıyla alışverişte kaç taksit yapabiliyorsunuz? | yanıt | ✅ | 1,3 sn |
| H18 | Normal | wifi baglantisi kopuyor, modem ile termostat arasi ne kadar olmali | yanıt | ✅ | 1,2 sn |
| H19 | Cevapsız | Lumora Termo'yu kombi yerine klimayla kullanabilir miyim? | bilgi yok | ✅ | 1,1 sn |
| H20 | Cevapsız | Fatura adresimi sipariş verdikten sonra değiştirebilir miyim? | bilgi yok | ✅ | 1,0 sn |
| H21 | Cevapsız | Termostatın çalışma sıcaklığı aralığı nedir? | bilgi yok | ✅ | 0,0 sn |
| H22 | Çelişkili | Cumartesi saat 20:00'de çağrı merkezine ulaşabilir miyim? | yanıt | ✅ | 1,6 sn |
| H23 | Çelişkili | Ambalajını hiç açmadığım ürünü teslim aldıktan 20 gün sonra iade edebilir miyim? | yanıt | ✅ | 1,6 sn |
| H24 | Çelişkili | İade ettiğim ürünün ücreti 7 iş günüdür hesabıma yatmadı. Para iadesi ne kadar sürmeli? | yanıt | ✅ | 1,9 sn |

## Halüsinasyon sinyalleri

Deterministik vekil ölçülerdir, kanıt değildir: cevapsız soruya yanıt, atıf yapılan dokümanlarda geçmeyen sayı, kaynakta birebir bulunamayan alıntı ve yasak ifade (ters karar, eski kural ya da bilgi tabanında olmayan genel bilgi). Sayı içermeyen ve yasak listesinde olmayan bir uydurma yakalanmaz; yanıtlar ayrıca elle okunur.

Hiçbir yanıtta sinyal yok.

## Soru bazında karşılaştırma

### H13 · Normal · ✅ geçti

**Soru:** Şirketim adına fatura kestirebilir miyim, hangi bilgileri vermem gerekiyor?  
**Beklenen:** Evet. Sipariş sırasında "Kurumsal fatura istiyorum" seçeneğini işaretleyip firma unvanı, vergi dairesi ve vergi numarasını girmeniz yeterlidir.  
**Gerçek:** Evet, kurumsal fatura alabilirsiniz. Sipariş sırasında "Kurumsal fatura istiyorum" seçeneğini işaretlemeniz; firma unvanı, vergi dairesi ve vergi numarasını girmeniz yeterlidir.  
**Kaynak:** `sss-genel` v1.0 (2024-02-01) › Sipariş ve Ödeme > Kurumsal fatura alabilir miyim? — "Evet. Sipariş sırasında "Kurumsal fatura istiyorum" seçeneğini işaretleyip firma unvanı, vergi dairesi ve vergi numarasını girmeniz yeterlidir." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ içerik · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 4,1 sn · **Model çağrısı:** 1

### H14 · Normal · ✅ geçti

**Soru:** Termostatın yazılımı ne zaman güncelleniyor, güncelleme sırasında nelere dikkat etmeliyim?  
**Beklenen:** Cihaz yazılımı her gece 03:00'te otomatik olarak güncellenir (manuel güncelleme: Cihaz Ayarları > Yazılım Güncelleme). Güncelleme sırasında LED turuncu yanıp söner; bu sırada cihazın elektriğini kesmeyin.  
**Gerçek:** Cihaz yazılımı her gece saat 03:00'te otomatik olarak güncellenmektedir. Güncelleme sırasında LED ışığı turuncu yanıp söner; bu süreçte cihazın elektriğini kesmemeniz gerekmektedir.  
**Kaynak:** `sorun-giderme-baglanti` v1.0 (2025-04-15) › 4. Yazılım (Firmware) Güncellemesi — "Cihaz yazılımı her gece 03:00'te otomatik olarak güncellenir. Manuel güncelleme için uygulamada Cihaz Ayarları > Yazılım Güncelleme adımını kullanın. Güncelleme sırasında LED turuncu yanıp söner; bu sırada cihazın elektriğini kesmeyin." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,8 sn · **Model çağrısı:** 1

### H15 · Normal · ✅ geçti

**Soru:** Kombim tamamen çalışmıyor çünkü termostat kombiyi çalıştırmıyor. Destek ekibi ne kadar sürede dönüş yapar?  
**Beklenen:** Isıtma sisteminin tamamen devre dışı kaldığı arızalar öncelikli kuyruğa alınır; teknik ekip 2 saat içinde geri dönüş yapar (Destek Kanalları v2.0, Öncelikli Destek).  
**Gerçek:** Isıtma sisteminin tamamen devre dışı kaldığı ve termostatın kombiyi çalıştırmadığı arızalar öncelikli kuyruğa alınır ve teknik ekip tarafından 2 saat içinde geri dönüş yapılır.  
**Kaynak:** `destek-kanallari-v2` v2.0 (2025-09-01) › 4. Öncelikli Destek — "Isıtma sisteminin tamamen devre dışı kaldığı arızalar (örneğin termostat kombiyi çalıştırmıyorsa) öncelikli kuyruğa alınır ve 2 saat içinde teknik ekip tarafından geri dönüş yapılır." *(alıntı doğrulandı)*  
**Sürüm kararı:** `destek-kanallari-v1` v1.0 elendi — 2.0 sürümü (2025-09-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,6 sn · **Model çağrısı:** 1

### H16 · Normal · ✅ geçti

**Soru:** Lumora Termo'nun kutusundan hangi parçalar çıkıyor?  
**Beklenen:** Lumora Termo cihazı, duvar montaj plakası, 2 adet vida ve dübel, kablo etiketleri ve hızlı başlangıç kartı.  
**Gerçek:** Lumora Termo kutusunun içeriğinde Lumora Termo cihazı, duvar montaj plakası, 2 adet vida ve dübel, kablo etiketleri ve hızlı başlangıç kartı bulunmaktadır.  
**Kaynak:** `kurulum-kilavuzu-lumora-termo` v1.0 (2025-02-01) › 1. Kutu İçeriği — "Kutuda Lumora Termo cihazı, duvar montaj plakası, 2 adet vida ve dübel, kablo etiketleri ve hızlı başlangıç kartı bulunur." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ içerik · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,5 sn · **Model çağrısı:** 1

### H17 · Normal · ✅ geçti

**Soru:** Kredi kartıyla alışverişte kaç taksit yapabiliyorsunuz?  
**Beklenen:** Anlaşmalı bankaların kredi kartlarına 6 aya kadar taksit seçeneği sunulur.  
**Gerçek:** Anlaşmalı bankaların kredi kartlarına 6 aya kadar taksit seçeneği sunulmaktadır.  
**Kaynak:** `sss-genel` v1.0 (2024-02-01) › Sipariş ve Ödeme > Taksitle ödeme yapabilir miyim? — "Anlaşmalı bankaların kredi kartlarına 6 aya kadar taksit seçeneği sunulmaktadır." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,3 sn · **Model çağrısı:** 1

### H18 · Normal · ✅ geçti

**Soru:** wifi baglantisi kopuyor, modem ile termostat arasi ne kadar olmali  
**Beklenen:** Cihaz ile modem arasındaki mesafeyi 10 metrenin altına indirin (ayrıca modemin 2.4 GHz yayın yaptığını kontrol edin; modemi kapatıp 30 saniye sonra yeniden açın).  
**Gerçek:** Cihaz ile modem arasındaki mesafeyi 10 metrenin altına indirmeniz önerilmektedir.  
**Kaynak:** `sorun-giderme-baglanti` v1.0 (2025-04-15) › 2. Cihaz Wi-Fi Ağına Bağlanmıyor — "Cihaz ile modem arasındaki mesafeyi 10 metrenin altına indirin." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,2 sn · **Model çağrısı:** 1

### H19 · Cevapsız · ✅ geçti

**Soru:** Lumora Termo'yu kombi yerine klimayla kullanabilir miyim?  
**Beklenen:** Dokümanlarda klima uyumluluğundan bahsedilmiyor (kurulum yalnızca kombi bağlantısını anlatıyor); bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Verilen kaynaklarda Lumora Termo'nun klima ile kullanımına dair bir bilgi bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 1,1 sn · **Model çağrısı:** 1

### H20 · Cevapsız · ✅ geçti

**Soru:** Fatura adresimi sipariş verdikten sonra değiştirebilir miyim?  
**Beklenen:** Dokümanlarda fatura adresi değişikliği yok; yalnızca teslimat adresinin kargoya verilmeden önce değiştirilebileceği anlatılıyor. Teslimat adresi kuralı fatura adresine uygulanmamalı; bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Sipariş verildikten sonra fatura adresinin değiştirilip değiştirilemeyeceğine dair bilgi kaynaklarda bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 1,0 sn · **Model çağrısı:** 1

### H21 · Cevapsız · ✅ geçti

**Soru:** Termostatın çalışma sıcaklığı aralığı nedir?  
**Beklenen:** Dokümanlarda cihazın teknik özellikleri (çalışma sıcaklığı aralığı) yok; bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: LowRelevance)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,0 sn · **Model çağrısı:** 0

### H22 · Çelişkili · ✅ geçti

**Soru:** Cumartesi saat 20:00'de çağrı merkezine ulaşabilir miyim?  
**Beklenen:** Evet. Güncel sürüme (v2.0) göre çağrı merkezi hafta sonu ve resmî tatiller dahil her gün 08:00–22:00 arasında hizmet verir. v1.0'daki 'hafta sonu destek verilmez' kuralı geçersiz.  
**Gerçek:** Evet, çağrı merkezimize hafta sonları dahil her gün 08:00–22:00 saatleri arasında 0850 000 00 00 numarasından ulaşabilirsiniz.  
**Kaynak:** `destek-kanallari-v2` v2.0 (2025-09-01) › 2. Telefon Desteği — "Çağrı merkezimize 0850 000 00 00 numarasından hafta sonu ve resmî tatiller dahil her gün 08:00–22:00 saatleri arasında ulaşabilirsiniz." *(alıntı doğrulandı)*  
**Sürüm kararı:** `destek-kanallari-v1` v1.0 elendi — 2.0 sürümü (2025-09-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ içerik · ✅ yasak ifade · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,6 sn · **Model çağrısı:** 1

### H23 · Çelişkili · ✅ geçti

**Soru:** Ambalajını hiç açmadığım ürünü teslim aldıktan 20 gün sonra iade edebilir miyim?  
**Beklenen:** Evet. Güncel politikaya (v2.0) göre teslimattan itibaren 30 gün içinde iade talebinde bulunulabilir. v1.0'daki 14 günlük süre geçersiz; eski kurala göre yanıt 'hayır' olurdu.  
**Gerçek:** Evet, ürünü teslim aldığınız tarihten itibaren 30 gün içinde iade talebinde bulunabilirsiniz. İade edilecek ürünün orijinal kutusunda gönderilmesi gerekmektedir.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 2. İade Süresi — "Müşteriler, ürünü teslim aldıkları tarihten itibaren 30 gün içinde iade talebinde bulunabilir." *(alıntı doğrulandı)*  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 3. İade Koşulları — "İade edilecek ürün orijinal kutusunda ve tüm aksesuarlarıyla birlikte gönderilmelidir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ içerik · ✅ yasak ifade · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,6 sn · **Model çağrısı:** 1

### H24 · Çelişkili · ✅ geçti

**Soru:** İade ettiğim ürünün ücreti 7 iş günüdür hesabıma yatmadı. Para iadesi ne kadar sürmeli?  
**Beklenen:** Güncel politikaya (v2.0) göre ürün depoya ulaşıp kontrol edildikten sonra ücret 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir; bankanın yansıtma süresi bu süreye dahil değildir. v1.0'daki 10 iş günü geçersiz.  
**Gerçek:** İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu 5 iş günlük süreye dahil değildir.  
**Eksik bilgi (model):** Ürünün depoya ulaşıp ulaşmadığı ve kontrol edilip edilmediği bilgisi verilmemiştir.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 6. Para İadesi — "İade edilen ürün depomuza ulaşıp kontrol edildikten sonra ücret, 5 iş günü içinde ödemenin yapıldığı karta veya hesaba iade edilir. Bankanızın iadeyi hesabınıza yansıtma süresi bu süreye dahil değildir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,9 sn · **Model çağrısı:** 1

