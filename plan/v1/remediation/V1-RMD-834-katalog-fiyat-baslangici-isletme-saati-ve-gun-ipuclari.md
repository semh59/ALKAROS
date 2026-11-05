# V1-RMD-834 - Katalog fiyat başlangıcı işletme saatiyle; rapor ve fatura ekranlarında gün ipucu

- Task ID: V1-RMD-834
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-832 incelemesinde çıkan iki kalan iş. (1) Katalogda yeni fiyatın "Geçerlilik başlangıcı" alanı cihazın yerel saatiyle dolar ve girilen saat cihaz diliminde yorumlanır; yanlış saat dilimli bir kasa fiyatı İstanbul saatine göre kaydırır. Düzeltme: alan İstanbul duvar saatiyle dolar ve girilen değer İstanbul saati (+03:00) olarak yorumlanıp anlık olarak gönderilir (`venueDateTimeInput`, `venueInstant`). (2) Kanal raporu ve ürün marjı raporu iş gününe (06:00 başlar), sipariş fatura listesi vergi hizmet tarihine (takvim günü) göre süzer ama ekran bunu söylemez; gece 00:00-06:00 arası kullanıcı iki ekranın "bugün"ünün farkını anlayamaz. Düzeltme: üç ekranın ipucu cümlesi hangi günü kullandığını söyler.

## Owned surface

- `evidence/V1-RMD-834/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/format.ts (V1-RMD-784) — `venueDateTimeInput`, `venueInstant`.
  - src/Clients/PosTerminal/src/format.test.ts (V1-RMD-784) — yardımcı testleri.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx (V1-RMD-097) — fiyat başlangıcı.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.test.tsx (V1-RMD-097) — fiyat başlangıcı testi.
  - src/Clients/PosTerminal/src/strings.ts (V1-RMD-097) — üç ipucu cümlesi.
  - src/Clients/PosTerminal/src/features/management-channel-report/ChannelReportSection.test.tsx (V1-RMD-466) — ipucu testi.
  - src/Clients/PosTerminal/src/features/management-product-margin-report/ProductMarginSection.test.tsx (V1-RMD-488) — ipucu testi.
  - src/Clients/PosTerminal/src/features/management-order-invoices/OrderInvoicesSection.test.tsx (V1-RMD-473) — ipucu testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe.

## Out of scope

- Sunucu; fiyatın saat dilimi alanı dışındaki alanlar.

## Dependencies

- V1-RMD-832

## Acceptance evidence

1. PosTerminal vitest tam paket, tsc yeşil: fiyat başlangıcı farklı cihaz saat dilimlerinde aynı İstanbul saatiyle dolar, gönderilen anlık İstanbul saatinden hesaplanır; üç ekranın ipucu doğru günü söyler.
2. Mutasyon: saat dilimi ve ipucu bozulunca test kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
