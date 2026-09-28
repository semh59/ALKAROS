# V1-RMD-398 — Stok/reçete akışı hedefli derin denetim raporu

- Tarih: 2026-09-28
- Denetlenen commit: `691bf94` (dal `claude/project-analysis-categorization-vt05a4`; master `cc25c0f` + V1-RMD-393…396,
  V14-RMD-001)
- Yürütücü: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Yöntem: V1-RMD-393 ile aynı — gerçek PostgreSQL 18 + gerçek `DualScreenApplication` üzerinde probe testleri; stok
  durumu gerçek servislerle (düzeltme, tüketim, sayım, fire, üretim, satın alma) üretildi; mutasyon denetimi; kör
  kalibrasyon; bağımsız çürütme. Üretim kodu, test projesi, migration **değiştirilmedi**.

## 1. Invariant listesi

| Kod | Invariant |
| --- | --- |
| S-INV-1 | Her (malzeme, lokasyon) için bakiye = hareket defterinin toplamı. |
| S-INV-2 | Bakiye hiçbir yazmada negatife düşmez; kullanılabilir miktar başka siparişlerin rezervasyonlarını düşer. |
| S-INV-3 | Void edilen satış kalemi tükettiği stoğu tam olarak geri verir ve teorik tüketimini de geri alır. |
| S-INV-4 | Fiziksel sayım bakiyeyi sayılan miktara **ayarlar**; sonuç bakiye sayılan miktardır. |
| S-INV-5 | Aynı idempotency anahtarıyla tekrarlanan fire/düzeltme/üretim tamamlama tek hareket yazar. |
| S-INV-6 | Birimler: fire faktörü reçetenin kendi biriminde uygulanır, sonra dönüştürülür (C9); dönüştürülemeyen birim sessizce yok sayılmaz. |
| S-INV-7 | Fark raporu: fiili kullanım ile teorik kullanım aynı olaylar kümesini kapsar (üretim, void, lokasyon). |
| S-INV-8 | Maliyet "as of D" = D iş günü sonuna kadar teslim alınanlar (restoranın yerel tarihi). |

## 2. Bulgular (probe ile doğrulandı)

| Kod | Probe | Özet | Önem | Doğrulayıcı |
| --- | --- | --- | --- | --- |
| G-01 | S01 | Fiziksel sayım mevcut bakiyeyi transaction/kilit **dışında** okuyup `sayılan − eski bakiye` farkını uyguluyor; araya giren bir stok yazması sayım sonucunu kaydırıyor (40 eşzamanlı denemede ~1; ör. 900 sayıldı, bakiye 899 kaldı). `PhysicalCountService.cs` | Düşük | KISMİ — `PhysicalCountService.cs:15-18` bu pencereyi bilinçli olarak kapsam dışı sayıyor; sayım zaten raf sayımı ile gönderim arasında satış kaymasına açık. |
| G-02 | S02 | Mutfağa gitmiş kalemin void'i stoğu geri veriyor ama `recipe.theoretical_consumption_records` satırını bırakıyor; fark raporunda yenmeyen yemek "beklenen kullanım" sayılıyor (0,4 kg sahte eksik). | Orta | DOĞRULANDI (S02 yeniden koşuldu); Sent/Held void için hiçbir belge davranışı tanımlamıyor. |
| G-03 | S03 | Fark raporu teorik kullanımı yalnız malzemeye göre gruplayıp her lokasyon satırına ekliyor; iki lokasyonda sayılan malzemede 3 kg teorik kullanım 6 kg görünüyor, kullanılmayan depoda −3 kg sahte sapma. | Düşük | KISMİ — `ReportingModels.cs:111-115` ve V11-RPT-003:48-52,67-68 "belgelenmiş sadeleştirme"; lokasyon düzeyi teorik kullanım kapsam dışı. |
| G-04 | S04 | Üretim partisi ölçeği `gerçekleşen / reçete verimi` — partinin birimi (`portion`) ile reçetenin verim birimi (`kg`) hiç karşılaştırılmıyor; "4 porsiyon" parti "2 kg" verimli reçeteden 2 katı malzeme tüketiyor. | Orta | DOĞRULANDI (S04 yeniden koşuldu); `yield_unit_code` okunuyor ama kullanılmıyor (`:124-139`), HTTP varsayılanı `portion`. |
| G-05 | S05 | Fark raporunun formülü `açılış + teslim − kapanış`; üretimin tükettiği malzeme açıklanamayan sapma görünüyor (5 kg). | Orta | DOĞRULANDI; V11-RPT-003 formülü üretim/transfer/fireyi hiç anmıyor. Üretim çıktısı da formülde yok. |
| G-06 | S06 | Satışta teorik tüketim, reçete malzemesinin birimi stok birimine dönüştürülemezse (`adet` → `kg`) sessizce atlanıyor: kayıt yok, hata yok, log yok. Üretim aynı durumu reddediyor. | Düşük | ÇÜRÜTÜLDÜ (hata olarak) — V11-RCP-004:64-67 dönüştürülemeyen satırı bilinçli olarak "sessizce atla" diyor (gölge defter siparişi bloklamamalı). Kalan tek nokta: atlama loglanmıyor. |
| G-07 | S08 | `InventoryAdjustmentRequest.IdempotencyKey` hiç okunmuyor; aynı anahtarla iki düzeltme iki kez düşüyor (27 yerine 26). Servisi bugün hiçbir HTTP uç noktası çağırmıyor. | Düşük (bugün) | DOĞRULANDI (ölü yol) — hiçbir uç nokta çağırmıyor. |
| G-08 | S13 | Hareketli ortalama maliyet `CAST(received_at AS date)` ile veritabanı oturumunun saat diliminde (dağıtımda ayarlanmamış → UTC) hesaplanıyor; İstanbul 01:30'daki teslim önceki günün maliyetine giriyor (30 yerine 60). | Düşük | DOĞRULANDI — dağıtımda saat dilimi ayarı yok; etki yalnız 00:00–03:00 İstanbul teslimleri ve sınır günü. |

## 2b. Yalnız kod okuması

| Kod | Özet | Önem |
| --- | --- | --- |
| G-09 | Satışta gerçek stok düşümü reçeteden değil ürün→stok eşlemesinden (çarpan) yapılıyor; reçete yalnız teorik tüketimi besliyor. **Bilinçli tasarım** (V11-RCP-004:10-16, "EK OLARAK"); fark raporu tam bu kaymayı göstermek için var. | Tasarım notu |
| G-10 | `IStockCostResolver.cs` üretim kodunda AI düşünme notları kalmış ("Wait, earlier we saw…", "Let's inspect…"). | Düşük |
| G-11 | Katalogda `StockMode.Untracked` var ama satış yolu hiç okumuyor; eşlemesi olmayan ürün V1-RMD-143 kararıyla reddediliyor. "Takipsiz" ürün kavramı fiilen yok. | Karar sorusu |
| G-12 | Void'de stok iadesi sipariş yazımından ayrı ve "best-effort" (`SentItemVoidStore.cs`); iade düşerse kalem iptal olur, stok geri gelmez (denetim kaydında `StockRestored=false` kalır). | Düşük |

## 3. Çürütülen hipotezler

| Hipotez | Neden çürüdü |
| --- | --- |
| Void, eşlemeyi yeniden hesaplayıp yanlış miktar iade eder | `RestoreStockForVoidedItemAsync` orijinal tüketim hareketlerini tersine çeviriyor (S11 bunu doğruladı). |
| Online sipariş kabulü ile teslimi arasında stok ayrılmaz | Kabulde `ICrossChannelPortionArbiter.ReserveAsync` ile rezervasyon yapılıyor. |
| Kayıtlı olmayan reçete birimi (`tbsp`) sessizce geçer | `AddIngredientToDraftAsync` bilinmeyen birimi reçete yazılırken reddediyor. |
| Fark raporu teslimleri kaçırır | Satın alma `GoodsReceipt` kaynak türü yazıyor; rapor bunu okuyor. |
| İlk S13 kurgusu (önceki günün gecesi) | Probe yanlış kurulmuştu; hata ertesi günün gecesinde görülür, probe buna göre düzeltildi. |

## 4. Mutasyon denetimi

⏳

## 5. Kör kalibrasyon

- Bağımsız ajanın yaması ve mühürlü açıklaması 09:22:11Z'de mühürlendi (`calibration/SEAL.sha256`); kodu ben okumaya
  başlamadan önce üretildi.
- Kör koşuda deterministik probe'ların hepsi yamalı/yamasız aynı sonucu verdi; tek fark S01'in rastgele yarış
  iterasyonuydu (gürültü). Otomatik karar "DETECTED-OR-DIFFERENT" yazdı; mühür açılmadan önce elle **KAÇIRILDI**
  olarak düzeltildi (`calibration/VERDICT-before-unseal.txt`).
- Hata: `IStockCostResolver.cs:49` `<=` → `<` — maliyet tarihinde teslim alınan mallar hareketli ortalamaya girmiyor.
- **Neden kaçtı:** görevin Goal'ünde "maliyet anlık görüntüsü" vardı ama hiç maliyet probe'u yazılmamıştı — doğrudan
  kapsam açığı (V1-RMD-393'teki "kardinalite" açığından sonra ikinci kez: matris, Goal'deki her adımı kapsamadı).
- Kapatma (kör değil): S12 eklendi; temiz kopyada geçer, yamalı kopyada 30 ≠ 40 ile düşer
  (`calibration/run-s12-clean-vs-seeded.log`). Aynı bölgeye bakarken yamadan bağımsız gerçek hata G-08 bulundu.
- Mevcut test paketinin yamayı yakalayıp yakalamadığı: mutasyon N08 (§4).

## 6. Bağımsız çürütme

Bağlamı bilmeyen ikinci bir ajana yalnız iddialar verildi; S02 ve S04'ü kendisi yeniden koşup doğruladı, diğerlerini
kod ve plan belgeleriyle değerlendirdi.

- Doğrulanan ve belgelenmemiş: G-02, G-04, G-05, G-07, G-08, G-10.
- **Belgelenmiş/bilinçli çıkanlar (benim iddialarımı düzeltti):** G-01 (kod yorumu kapsam dışı sayıyor), G-03
  (belgelenmiş sadeleştirme), G-06 (V11-RCP-004 "sessizce atla" diyor — hata değil, yalnız log eksik), G-09
  (bilinçli iki model).
- Önem düzeltmeleri: G-04 Yüksek → Orta; G-01, G-03, G-06, G-08 → Düşük.
- Ders: bu turda "bulgu" dediğim 8 probe'un 3'ü plan belgelerinde kabul edilmiş davranışlardı. V1-RMD-393'te bu oran
  daha düşüktü; stok alanında plan kararlarının kodu okumadan önce taranması gerekirdi.

## 7. Kapsam matrisi

`Snn` geçen probe (sağlam) · `G-nn` bulgu · `Nnn` mutasyon · `K` kod okuması · `—` denetlenmedi.

| Adım \ Boyut | Mutlu yol | Tekrar/idempotency | Eşzamanlılık | Kardinalite | Yanlış durum/birim | Hata yolu/atomiklik |
| --- | --- | --- | --- | --- | --- | --- |
| Satış tüketimi | S07, S11 | — | S07 | S11 | G-06 | K (tek transaction) |
| Void iadesi | S11 | K | — | S11 | G-02 | G-12 (K) |
| Fiziksel sayım | S03, S05 | — | G-01 | — | — | K |
| Fire | S08 | S08 | S08 | — | K (kaynak listesi) | — |
| Manuel düzeltme | S08 | G-07 | — | — | — | — |
| Üretim | S10 | S09 | S09 | — | G-04 | K |
| Satın alma teslimi | S12, S13 | — | — | — | — | — |
| Maliyet | S12 | — | — | — | G-08 | — |
| Fark raporu | — | — | — | G-03 | G-05, G-02 | — |

## 8. Denetlenemeyenler

- Porsiyon rezervasyonu (QR/online kabul) uçtan uca HTTP üzerinden sınanmadı; yalnız okuma (§3).
- Satın alma: fazla teslim, kısmi teslim, iade — sınanmadı.
- Günlük menü sayaçları (Menu/CounterProjection) — kapsam dışında kaldı.
- İstemci ekranları — okunmadı.

## 9. Önerilen düzeltme görevleri

1. G-04: üretimde parti birimi ile reçete verim birimini dönüştür ya da reddet.
2. G-02, G-05: fark raporunu void ve üretimle tutarlı hale getir (aynı sorgu; G-03 sadeleştirmesi belgelendiği için ayrı karar).
3. G-08: maliyet tarihini restoranın yerel iş gününe göre hesapla (ödeme mutabakat raporuyla aynı tanım).
4. G-06: atlanan teorik tüketim satırını logla (davranış bilinçli, iz yok).
5. G-01, G-07, G-10, G-12: düşük öncelikli temizlik.
6. G-11: Semih'e karar sorusu (`StockMode.Untracked`).
