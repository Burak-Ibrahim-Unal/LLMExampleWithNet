# Değerlendirme Raporu

- **Tarih:** 2026-10-02 07:48
- **Çalıştırma:** holdout
- **Dil modeli:** gemma-4-26b-a4b-it · **Embedding:** bge-m3 · **Arama modu:** hybrid
- **Sonuç:** 10/12 soru geçti

## Özet

| Kategori | Soru | Geçen |
|---|---:|---:|
| Normal | 5 | 3 |
| Cevapsız | 3 | 3 |
| Çelişkili | 4 | 4 |
| **Toplam** | **12** | **10** |

**Arama isabeti** (beklenen kaynak, sunucunun varsayılan topK değeri kadar arama sonucu içinde — sürüm çözümünden önce; 9 soru): yalnız BM25 9/9 · hibrit 9/9  
**Yanıt süresi:** medyan 1,5 sn, ortalama 1,4 sn, en uzun 2,2 sn (Kapı 1'de reddedilen sorular modele gitmediği için ~0 sn)  
**Düzeltme turu:** 0 soruda model ikinci kez çağrıldı

| ID | Kategori | Soru | Beklenen | Sonuç | Süre |
|---|---|---|---|---|---:|
| H01 | Normal | sifremi unuttum uygulamaya giris yapamiyorum ne yapmaliyim | yanıt | ✅ | 1,8 sn |
| H02 | Normal | 749 TL'lik bir sipariş verdim, kargo için ücret ödeyecek miyim? | yanıt | ✅ | 1,4 sn |
| H03 | Normal | Termostatımı 2 yıl 3 ay önce aldım ve bozuldu. Garanti kapsamında ücretsiz onarılır mı? | yanıt | ❌ | 1,3 sn |
| H04 | Normal | Lumora Termo hangi Wi-Fi frekansında çalışır ve Amazon Alexa ile kullanılabilir mi? | yanıt | ✅ | 1,6 sn |
| H05 | Normal | Kargo takip numarası bana nasıl iletilir? Sipariş kargoya verildikten sonra teslimat adresini değiştirebilir miyim? | yanıt | ❌ | 1,7 sn |
| H06 | Cevapsız | Termostatın kurulumunu sizin teknisyeniniz evime gelip yapabilir mi, ücreti ne kadar? | bilgi yok | ✅ | 0,0 sn |
| H07 | Cevapsız | Lumora Termo'nun pili kaç ay dayanır? | bilgi yok | ✅ | 1,0 sn |
| H08 | Cevapsız | Garantisi biten cihazımı ücretli olarak onarıyor musunuz, onarım ücreti ne kadar? | bilgi yok | ✅ | 0,9 sn |
| H09 | Çelişkili | İade talebimi e-posta ile gönderebilir miyim? | yanıt | ✅ | 1,9 sn |
| H10 | Çelişkili | Ürünü geri gönderirken kargo parasını ben mi ödüyorum? | yanıt | ✅ | 2,2 sn |
| H11 | Çelişkili | Destek ekibine e-posta atarsam ne kadar sürede cevap gelir? | yanıt | ✅ | 1,4 sn |
| H12 | Çelişkili | Kutusunu açıp kurulumunu yaptığım termostatı iade edebilir miyim? | yanıt | ✅ | 2,2 sn |

## Soru bazında karşılaştırma

### H01 · Normal · ✅ geçti

**Soru:** sifremi unuttum uygulamaya giris yapamiyorum ne yapmaliyim  
**Beklenen:** Uygulamanın giriş ekranındaki "Şifremi Unuttum" bağlantısına dokunun; şifre sıfırlama bağlantısı e-posta adresinize gönderilir.  
**Gerçek:** Lumora uygulamasının giriş ekranındaki "Şifremi Unuttum" bağlantısına dokunmanız yeterlidir; şifre sıfırlama bağlantısı e-posta adresinize gönderilecektir.  
**Kaynak:** `sss-genel` v1.0 (2024-02-01) › Hesap > Şifremi unuttum, ne yapmalıyım? — "Lumora uygulamasının giriş ekranındaki "Şifremi Unuttum" bağlantısına dokunun; şifre sıfırlama bağlantısı e-posta adresinize gönderilir." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,8 sn · **Model çağrısı:** 1

### H02 · Normal · ✅ geçti

**Soru:** 749 TL'lik bir sipariş verdim, kargo için ücret ödeyecek miyim?  
**Beklenen:** Evet. 750 TL'nin altındaki siparişlerde 49,90 TL kargo ücreti alınır; kargo yalnızca 750 TL ve üzerindeki siparişlerde ücretsizdir.  
**Gerçek:** 750 TL'nin altındaki siparişler için 49,90 TL kargo ücreti alınmaktadır.  
**Kaynak:** `kargo-ve-teslimat` v1.0 (2025-03-01) › 2. Kargo Ücreti — "750 TL'nin altındaki siparişler için 49,90 TL kargo ücreti alınır." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,4 sn · **Model çağrısı:** 1

### H03 · Normal · ❌ kaldı

**Soru:** Termostatımı 2 yıl 3 ay önce aldım ve bozuldu. Garanti kapsamında ücretsiz onarılır mı?  
**Beklenen:** Hayır. Garanti süresi fatura tarihinden itibaren 2 yıldır; 2 yıl 3 ay geçtiği için cihaz artık garanti kapsamında değildir.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Ürünün garanti kapsamına girip girmediğini belirlemek için arızanın nedeninin (üretim/malzeme hatası mı yoksa kullanım hatası mı olduğu) bilinmesi gerekmektedir. Ayrıca cihazın Lumora Termo olup olmadığı belirtilmemiştir; ancak Lumora Termo ise 2 yıllık garanti süresi dolmuştur.  
**Kontroller:** ❌ yanıtlanabilirlik (beklenen: yanıt, gerçek: bilgi yok (ModelInsufficientContext))  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,3 sn · **Model çağrısı:** 1

### H04 · Normal · ✅ geçti

**Soru:** Lumora Termo hangi Wi-Fi frekansında çalışır ve Amazon Alexa ile kullanılabilir mi?  
**Beklenen:** Yalnızca 2.4 GHz Wi-Fi ağlarını destekler (5 GHz desteklenmez). Alexa uyumluluğu hakkında dokümanlarda bilgi yok; bu kısım yanıtlanamaz (kısmi yanıt).  
**Gerçek:** Lumora Termo yalnızca 2.4 GHz Wi-Fi ağlarını desteklemektedir; 5 GHz ağlara bağlanamaz.  
**Eksik bilgi (model):** Amazon Alexa ile uyumluluğu hakkında bilgi verilmemiştir.  
**Kaynak:** `kurulum-kilavuzu-lumora-termo` v1.0 (2025-02-01) › 2. Kurulumdan Önce — "Lumora Termo yalnızca 2.4 GHz Wi-Fi ağlarını destekler; 5 GHz ağlara bağlanamaz." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,6 sn · **Model çağrısı:** 1

### H05 · Normal · ❌ kaldı

**Soru:** Kargo takip numarası bana nasıl iletilir? Sipariş kargoya verildikten sonra teslimat adresini değiştirebilir miyim?  
**Beklenen:** Takip numarası sipariş kargoya verildiğinde SMS ve e-posta ile gönderilir (Siparişlerim ekranından da takip edilebilir). Kargoya verilmiş siparişlerde adres değişikliği yapılamaz.  
**Gerçek:** Siparişiniz kargoya verildiğinde takip numaranız SMS ve e-posta yoluyla iletilir. Kargoya verilmiş siparişlerde adres değişikliği yapılamamaktadır.  
**Kaynak:** `kargo-ve-teslimat` v1.0 (2025-03-01) › 3. Sipariş Takibi — "Siparişiniz kargoya verildiğinde takip numarası SMS ve e-posta ile gönderilir." *(alıntı doğrulandı)*  
**Kaynak:** `kargo-ve-teslimat` v1.0 (2025-03-01) › 5. Adres Değişikliği — "Kargoya verilmiş siparişlerde adres değişikliği yapılamaz." *(alıntı doğrulandı)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ❌ içerik ('yapılamaz' | 'değiştirilemez' | 'değiştiremezsiniz' | 'mümkün değil' | 'hayır')  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,7 sn · **Model çağrısı:** 1

### H06 · Cevapsız · ✅ geçti

**Soru:** Termostatın kurulumunu sizin teknisyeniniz evime gelip yapabilir mi, ücreti ne kadar?  
**Beklenen:** Dokümanlarda yerinde kurulum hizmeti veya ücreti hakkında bilgi yok; bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: LowRelevance)*  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,0 sn · **Model çağrısı:** 0

### H07 · Cevapsız · ✅ geçti

**Soru:** Lumora Termo'nun pili kaç ay dayanır?  
**Beklenen:** Dokümanlarda pil veya pil ömrüyle ilgili bilgi yok; bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Lumora Termo'nun pil ömrü veya pil kullanım süresiyle ilgili bilgi kaynaklarda bulunmamaktadır.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 1,0 sn · **Model çağrısı:** 1

### H08 · Cevapsız · ✅ geçti

**Soru:** Garantisi biten cihazımı ücretli olarak onarıyor musunuz, onarım ücreti ne kadar?  
**Beklenen:** Dokümanlarda garanti dışı ücretli onarım hizmeti veya ücreti hakkında bilgi yok; bilgi olmadığı belirtilmeli.  
**Gerçek:** Bu soruyu yanıtlamak için dokümanlarda yeterli bilgi bulunamadı. *(refusalReason: ModelInsufficientContext)*  
**Eksik bilgi (model):** Garantisi biten cihazların ücretli onarımı ve onarım ücretinin miktarı hakkında bilgi verilmemiştir.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ ret sözleşmesi  
**Arama isabeti:** BM25 — · hibrit — · **Süre:** 0,9 sn · **Model çağrısı:** 1

### H09 · Çelişkili · ✅ geçti

**Soru:** İade talebimi e-posta ile gönderebilir miyim?  
**Beklenen:** Hayır. Güncel politikaya (v2.0) göre iade talebi Lumora uygulamasında Siparişlerim > İade Talebi Oluştur adımlarıyla oluşturulur; e-posta ile gönderilen talepler işleme alınmaz. v1.0'daki e-posta yöntemi geçersiz.  
**Gerçek:** Hayır, e-posta ile gönderilen iade talepleri işleme alınmamaktadır. İade talebinizi Lumora mobil uygulaması üzerinden Siparişlerim > ilgili sipariş > İade Talebi Oluştur adımlarını izleyerek oluşturmanız gerekmektedir.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 4. İade Talebi Nasıl Oluşturulur? — "E-posta ile gönderilen iade talepleri işleme alınmaz." *(alıntı doğrulandı)*  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 4. İade Talebi Nasıl Oluşturulur? — "Lumora mobil uygulamasında Siparişlerim > ilgili sipariş > İade Talebi Oluştur adımlarını izleyin." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ içerik · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,9 sn · **Model çağrısı:** 1

### H10 · Çelişkili · ✅ geçti

**Soru:** Ürünü geri gönderirken kargo parasını ben mi ödüyorum?  
**Beklenen:** Hayır. Güncel iade politikasına (v2.0) göre iade koduyla anlaşmalı kargoyla gönderilen iadelerde kargo ücretsizdir; ücreti Lumora karşılar. SSS'deki "müşteri öder" bilgisi eskidir.  
**Gerçek:** İade kodunu kullanarak anlaşmalı kargo firmamızla gönderdiğiniz iadelerin kargo ücretini Lumora karşılamaktadır.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 5. İade Kargo Ücreti — "iade kodunu kullanarak anlaşmalı kargo firmamızla gönderdiğiniz iadelerin kargo ücretini Lumora karşılar." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kaynaklar arası çelişki:** İade kargo ücreti — seçilen `iade-politikasi-v2`, elenen `sss-genel`; kurala uygun: evet  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ yasak ifade  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 2,2 sn · **Model çağrısı:** 1

### H11 · Çelişkili · ✅ geçti

**Soru:** Destek ekibine e-posta atarsam ne kadar sürede cevap gelir?  
**Beklenen:** Güncel sürüme (v2.0) göre e-postalara 24 saat içinde ilk yanıt verilir; v1.0'daki 48 saat geçersiz.  
**Gerçek:** Destek@lumora.example adresine gönderilen e-postalara 24 saat içinde ilk yanıt verilir.  
**Kaynak:** `destek-kanallari-v2` v2.0 (2025-09-01) › 3. E-posta Desteği — "destek@lumora.example adresine gönderilen e-postalara 24 saat içinde ilk yanıt verilir." *(alıntı doğrulandı)*  
**Sürüm kararı:** `destek-kanallari-v1` v1.0 elendi — 2.0 sürümü (2025-09-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ sayılar kaynakta · ✅ içerik · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 1,4 sn · **Model çağrısı:** 1

### H12 · Çelişkili · ✅ geçti

**Soru:** Kutusunu açıp kurulumunu yaptığım termostatı iade edebilir miyim?  
**Beklenen:** Evet. Güncel politikaya (v2.0) göre ambalajı açılmış ve kurulumu yapılmış ürünler eksiksiz ve hasarsız olmaları kaydıyla iade edilebilir; göndermeden önce cihaz uygulamadaki hesaptan kaldırılıp fabrika ayarlarına döndürülmelidir. v1.0'daki "yalnızca üretim hatası varsa" kuralı geçersiz.  
**Gerçek:** Evet, ambalajı açılmış ve kurulumu yapılmış ürünler, eksiksiz ve hasarsız olmaları kaydıyla iade edilebilir. İade edilecek ürünün orijinal kutusunda ve tüm aksesuarlarıyla birlikte gönderilmesi gerekmektedir. Ayrıca cihazı göndermeden önce Lumora uygulamasında hesabınızdan kaldırmanız ve fabrika ayarlarına döndürmeniz gerekmektedir.  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 3. İade Koşulları — "Ambalajı açılmış ve kurulumu yapılmış ürünler de eksiksiz ve hasarsız olmaları kaydıyla iade edilebilir." *(alıntı doğrulandı)*  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 3. İade Koşulları — "İade edilecek ürün orijinal kutusunda ve tüm aksesuarlarıyla birlikte gönderilmelidir." *(alıntı doğrulandı)*  
**Kaynak:** `iade-politikasi-v2` v2.0 (2025-06-01) › 3. İade Koşulları — "Cihazı göndermeden önce Lumora uygulamasında hesabınızdan kaldırın ve fabrika ayarlarına döndürün." *(alıntı doğrulandı)*  
**Sürüm kararı:** `iade-politikasi-v1` v1.0 elendi — 2.0 sürümü (2025-06-01) tarafından geçersiz kılındı.  
**Kontroller:** ✅ yanıtlanabilirlik · ✅ kaynak · ✅ yasak kaynak yok · ✅ bölüm · ✅ alıntı doğrulandı · ✅ içerik · ✅ yasak ifade · ✅ eski sürüm elendi  
**Arama isabeti:** BM25 ✅ · hibrit ✅ · **Süre:** 2,2 sn · **Model çağrısı:** 1

