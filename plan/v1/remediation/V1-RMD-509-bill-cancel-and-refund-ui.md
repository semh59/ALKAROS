# V1-RMD-509 - Hesap iptali ve iade arayüzü

- Task ID: V1-RMD-509
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-13

## Goal

Kasa ekranında gerekçeli "Hesabı iptal et" düğmesi, ödenmiş hesapta "İade iste", müdür için bekleyen iade onay listesi ve kart iadesi sonuç girişi. Kapsam taslaktır; başlamadan önce Owned surface keşifle netleştirilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-509-bill-cancel-and-refund-ui.md`
- `evidence/V1-RMD-509/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/split-payment-refunds.test.js
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Uç sözleşmesi V1-RMD-506/508'de sabittir: iptal `POST .../billing/bills/{id}/cancel`, iade `.../billing/bills/{id}/refund-requests` ve `.../billing/refund-requests/{id}/approve|reject|complete`.
- Kasa hesap sayfası (`split-payment`): gerekçeli hesap iptali, ödeme başına iade isteme, bu hesabın iade isteklerini listeleme, onaylama/reddetme ve elle iade sonucunu fiş numarasıyla tamamlama; Türkçe metinler.

## Out of scope

- PosTerminal (React) hesap ekranı (V1-RMD-510); sunucu uçları (V1-RMD-506/508); iade istekleri `payments.refund_requests` tablosunda tutulur, ekran yalnız uçları çağırır.

## Dependencies

- V1-RMD-508

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-509/` altındadır.

## Handoff

- None
