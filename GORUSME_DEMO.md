# Görüşme demosu — 20 dakikalık canlı akış

Bu senaryo 2 Ekim 2026'da canlı olarak koşuldu; beklenen çıktılar gerçek yanıtlardan alındı
(uzak llama.cpp sunucusu: gemma-4-26B-A4B-it + bge-m3). Kod değişirse `dotnet run --project
tools/SupportAssistant.Eval` ile ana seti koşup buradaki beklentileri tazeleyin.

## 0) Hazırlık (2-3 dk)

```bash
# Model sunucuları (ayrı makinede olabilir; .env'deki BaseUrl'lere bakın)
#   sohbet:  llama-server -m gemma-4-26B-A4B-it-qat-UD-Q4_K_XL.gguf --jinja --port 1234 ...
#   embedding: llama-server -m bge-m3-Q8_0.gguf --embedding --pooling cls --port 1235 ...

dotnet run --project src/API/SupportAssistant.API
# Log: "Knowledge base indexed: 10 documents, 53 sections, hybrid retrieval."
# Arayüz: http://localhost:5031/scalar   ·   Durum: http://localhost:5031/v1/health
```

`/v1/health` `ok` gösterir: bu **yapılandırma** durumudur, model sunucusuna ağ çağrısı yapılmaz.

## 1) Normal soru — alıntı doğrulaması ve ASCII yazım (2 dk)

```bash
curl -s -X POST http://localhost:5031/v1/questions -H "Content-Type: application/json" \
  -d '{"question": "termostati fabrika ayarlarina nasil donduruyorum"}'
```

**Neye bakılır:** `sources[0]` → `kurulum-kilavuzu-lumora-termo › 4. Fabrika Ayarlarına Döndürme`,
`quoteVerified=true`. Türkçesiz yazım Türkçe normalizasyonla aynı terimlere iner.
Canlı sonuç: "…sıfırlama düğmesine 10 saniye boyunca basılı tutun…" (~2 sn).

## 2) Çelişkili sürümler — sürüm kararı kodda (3 dk)

```bash
curl -s -X POST http://localhost:5031/v1/questions -H "Content-Type: application/json" \
  -d '{"question": "Bir ürünü kaç gün içinde iade edebilirim?"}'
```

**Neye bakılır:** yanıt **30 gün** (v2.0); `versionResolution.discarded` içinde
`iade-politikasi-v1` ve gerekçesi ("2.0 sürümü tarafından geçersiz kılındı"). v1'deki 14 günlük
kural modele hiç gönderilmez — karar prompt'ta değil, `VersionResolver`'da.

## 3) Kaynaklar arası çelişki — öncelik kuralı zorlanır (3 dk)

```bash
curl -s -X POST http://localhost:5031/v1/questions -H "Content-Type: application/json" \
  -d '{"question": "İade kargo ücretini kim öder?"}'
```

**Neye bakılır:** yanıt "Lumora karşılar" (politika v2.0); `conflicts[]` → seçilen
`iade-politikasi-v2`, elenen `sss-genel`, `ruleSatisfied=true`. Model eski SSS'yi seçseydi
sunucu kaybeden bölümü bağlamdan çıkarıp bir kez daha çağırır, sürerse `UnresolvedConflict`
ile reddederdi.

## 4) "Bilgi yok" — üç kapı (4 dk)

```bash
# Kapı 1: ucuz ret, model çağrılmaz
curl -s -X POST http://localhost:5031/v1/questions -H "Content-Type: application/json" \
  -d '{"question": "Lumora'"'"'nın genel müdürü kimdir?"}'
# → LowRelevance, diagnostics.modelCalls: 0, ~100 ms

# Kapı 2: model kararı
curl -s -X POST http://localhost:5031/v1/questions -H "Content-Type: application/json" \
  -d '{"question": "Lumora Termo'"'"'yu Apple HomeKit ile kullanabilir miyim?"}'
# → ModelInsufficientContext + missingInformation
```

**Neye bakılır:** ret her zaman 200 + sabit mesaj + boş kaynaklar + `refusalReason`;
kapı 1'de model hiç çağrılmadığı için maliyet sıfır.

## 5) Ölçüm ve sınırlar (3 dk)

```bash
# Arama isabetini katman katman göster: aynı soru iki modda
curl -s "http://localhost:5031/v1/search?q=parami%20ne%20zaman%20geri%20alirim&mode=lexical"
curl -s "http://localhost:5031/v1/search?q=parami%20ne%20zaman%20geri%20alirim&mode=hybrid"
```

**Anlatılacaklar:** soruda "iade" geçmez; yalnız BM25 yanlış dokümana gider, vektör iade
politikasını bulur (ölçülmüş katkı: 10/12 → 12/12). 16 kalibrasyon + 2×12 bağımsız +
12 halüsinasyon sorusu deterministik kontrollerle `eval/results/` altında; halüsinasyon
setinde uydurma yok, hatalar fazla temkinli retler (H03, HL07, HL11).

## Muhtemel zorlayıcı sorular ve kısa cevaplar

- **"Benzerlik skoru cevaplanabilirlik mi?"** — Hayır; Kapı 1 kanıt eşiğidir, karar yine de
  modelin `answerable`'ında ve alıntı doğrulamasındadır.
- **"JSON şeması doğruluğu garantiler mi?"** — Garanti etmez; şema yalnızca ayrıştırma
  garantisi verir. Gerçek denetim: doğrulanmış alıntı + öncelik kuralı + değerlendirme.
- **"Model kaç kez çağrılıyor?"** — En fazla 2 gerçek sohbet çağrısı (JSON + alıntı/öncelik
  düzeltmeleri ortak bütçede); `diagnostics.modelCalls` gerçek sayıyı gösterir.
- **"16/16 neden küçük?"** — Sorular ve dokümanlar aynı kişi tarafından yazıldı; bu yüzden
  ayar için hiç kullanılmayan bağımsız setler eklendi (10/12, 12/12) ve kalan hatalar
  gizlenmeden raporlandı.
