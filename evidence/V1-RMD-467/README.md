# V1-RMD-467 - KDV dahil satır fiyatı: sipariş ve adisyon çekirdeği

Menü fiyatı KDV dahildir (PO 2026-09-30). Satır hesabı tek kuralla değişti: brüt = adet x birim fiyat + seçenekler - indirim,
KDV = brüt x oran / (100 + oran) (önce KDV, yarım yukarı), net = brüt - KDV.

## Değişiklikler

- `OrderMath.TaxIncludedIn` + `OrderItem` kurucusu (`ChangeQuantity` dahil): brütten böler.
- İkram satırı: indirim = `LineSubtotalValue` (sipariş), `BillItem.FromOrderItem` brüt + indirim ile çapraz denetler.
- `BillItem` (ara toplam = brüt + indirim, `TaxIncludedIn`), `BillAdjustment` (indirim ve servis bedeli aynı yardımcıyı kullanır).
- Kayıtlı satırlar yeniden hesaplanmaz; DB kısıtları biçimden bağımsızdır, göç yok (gerçek veri yok).

## Kanıt

- `red-*.log`: değişiklikten sonra beklenen eski üste-ekleme testlerinin kırmızısı (Orders 6, BillFoundation 9, SplitDesign 4, Host Billing 3).
- `green-*.log`, `run-*.log`: beklentiler KDV dahil güncellendikten sonra Orders, Billing (Foundation, Adjustments, PaymentClosure, SplitDesign), Host Billing yeşil.
- `suite-summary.txt`: ilgili 27 test projesinin taraması; tek kırmızı Host Billing idi (düzeltildi, `green-Host.Experience.Billing.log`).
  `Host/MigrationComposition/DualScreen` klasörü csproj içermez (üst klasör tarama hatası, ürün değil).
- `mut/mutant-*.log`: mutasyon (bölmeden `+ 100` kaldırıldı = eski üste ekleme) Orders 5, BillFoundation 4, Adjustments 4 testi kırmızı yapar;
  dosyalar geri alındı ve `cmp` ile özdeş doğrulandı.
- `real-host-trial.live.log`: gerçek Host (Postgres 55432, E2E tohumu, %10 KDV profilli 100 TL ürün): sipariş `subtotal 100, tax 9.09, total 100`,
  kalem `net 90.91, tax 9.09, gross 100`, adisyon `payableAmount 100, taxTotal 9.09`. Eskiden 110 / 10 idi.

## Açık kalan

- Host/raporlama/çift ekran (`V1-RMD-468`) ve istemci etiketleri (`V1-RMD-469`) sonraki görevlerdedir; o zamana dek kanal raporu net tutarı brüt olarak görünür.
