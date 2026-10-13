# V1-RMD-511 - İade edilen siparişin sipariş tabanlı raporlardan çıkması

- Task ID: V1-RMD-511
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

Hesabın tüm ödemeleri iade edilince hesap `Cancelled` olur ama sipariş `Completed` kalır. Ciro (ödeme tabanlı) bunu doğru düşer; sipariş tabanlı iki sayı hâlâ iade edilmiş siparişi sayar: gün sonundaki sipariş adedi ve garsonun vardiya özeti (satış toplamı, bahşiş havuzu garson sayısı, bahşiş toplamı). Bu görev iade edilmiş siparişi bu sayılardan çıkarır. Ürün marjı raporu zaten ödenmiş hesap tabanlıdır, değişmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-511-refunded-orders-out-of-order-based-reports.md`
- `evidence/V1-RMD-511/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/BusinessDayTotals/BusinessDayTotalsReader.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/ShiftSummaryStore.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Reporting/BusinessDayOrderCountRefundTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/ShiftSummaryRefundTests.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- "İade edilmiş sipariş": iptal edilmiş ve iade edilmiş ödemesi olan bir hesabı bulunan, ödenmiş hesabı olmayan sipariş.
- Gün sonu sipariş adedi, vardiya özeti satış toplamı, bahşiş havuzu garson sayısı ve bahşiş toplamı bu siparişleri saymaz; kısmen iade edilmiş ama hâlâ açık hesabın siparişi sayılmaya devam eder.

## Out of scope

- Kanal (QR/Online) raporu: sağlayıcı iadeleri modellenmiyor; stok tüketimini iade ile geri almak (ürün kararı: yapılmış yemek geri alınmaz).

## Dependencies

- V1-RMD-508

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-511/` altındadır.

## Handoff

- None
