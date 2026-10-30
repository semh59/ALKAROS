# V1-RMD-757 - Bilgi fişi yalnız onaylanmış iadeyi "İade edildi" diye basar

- Task ID: V1-RMD-757
- Status: Planned
- Assignee: hermes-session-20261028_015834_6948d340
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu F0077 (register.md:87): müşteriye basılan bilgi fişi `refundedQuantity` alanını "İade edildi: N adet" diye basıyor. Bu alan reddedilmemiş her isteği (Requested, AwaitingManualRefund, Completed) topluyor; onay bekleyen veya sonradan reddedilebilecek iade fişte gerçekleşmiş gibi görünüyor. Fişte yalnız `Completed` iade adedi basılmalı. Semih kararı (2026-10-30): bekleyen iade fişe hiç basılmaz.

`refundedQuantity` kalan-adet hesabı için (V1-RMD-525: kasiyer ve PosTerminal iade ekranı) bekleyen istekleri saymak zorundadır; bu alanın anlamı değişmez. Fiş için ayrı, yalnız tamamlanmış iadeyi taşıyan alan eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-757-bilgi-fisi-yalniz-onayli-iade.md`
- `evidence/V1-RMD-757/**`
- Sınırlı ek: src/Host/DualScreen/DualScreenApplication.Receipt.cs
- Sınırlı ek: src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek: tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
- Sınırlı ek: tests/Clients/StaticApps/split-payment-receipt.test.js

## In scope

- `/receipt` yanıtında her kalemde yeni `completedRefundedQuantity`: yalnız `Completed` isteklerdeki adet toplamı. `refundedQuantity` aynı kalır.
- Cashier bilgi fişi "İade edildi" satırını `completedRefundedQuantity` ile basar.
- Gerçek PostgreSQL Host testi: Completed + Rejected + Requested + AwaitingManualRefund isteklerinde `refundedQuantity` 3 bekleyen dahil, `completedRefundedQuantity` yalnız Completed.
- İstemci testi: yalnız bekleyen iadesi olan kalemde fişte "İade edildi" satırı yok.

## Out of scope

- PosTerminal (React) `RefundPanel` ve `refundApi.ts`: `refundedQuantity` anlamı değişmediği için dokunulmaz.
- İade isteği oluşturma, kalan adet doğrulaması (`PaymentRefunds.cs`).
- Fişte bekleyen iadenin ayrı satır olarak gösterilmesi (Semih reddetti).

## Dependencies

- V1-RMD-516
- V1-RMD-525
- V1-RMD-526

## Acceptance evidence

1. Düzeltmeden önce yeni Host ve istemci testleri kırmızı olur; çıktı kaydedilir.
2. Düzeltmeden sonra `BillReceiptHttpTests` (gerçek PostgreSQL) ve `split-payment-receipt.test.js` ve `split-payment-item-refund.test.js` exit 0 geçer; Host derlemesi 0 uyarı 0 hata.
3. Mutasyon: SQL `FILTER` koşulu kaldırıldığında Host testi, istemci `completedRefundedQuantity` yerine `refundedQuantity` basarsa istemci testi kırmızı olur; dosya hash'leri geri yüklenir.
4. Kapsam aracı InProgress baseline'a karşı exit 0 verir; iki bağımsız incelemede artık bulgu kalmaz.

## Handoff

- None
