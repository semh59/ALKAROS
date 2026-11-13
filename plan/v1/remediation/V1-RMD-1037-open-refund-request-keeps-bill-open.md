# V1-RMD-1037 - Onay bekleyen iade isteği olan hesap, kalan tutar ödense bile kapanmaz; ekran nedenini Türkçe söyler

- Task ID: V1-RMD-1037
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Hesapta durumu "Requested" (onay bekliyor) olan bir iade isteği varken hesap kapanış hesabı, bekleyen/bilinmeyen ödeme engelleriyle aynı yolla yeni bir engel (HasOpenRefundRequest) bildirir ve hesabı "Ödendi" yapmaz; yönetici isteği onaylayıp ya da reddedince mevcut kapanış süpürücüsü hesabı kapatır. Ödeme ekranı bu durumda "Hesap Ödendi" yerine nedenini Türkçe söyler: ödeme tamam, onay bekleyen iade isteği olduğu için hesap açık, yönetici karar verince kapanır. PO kararı Semih 2026-11-13 (kasiyer denemesi 6, sorun 4).

## Owned surface

- `evidence/V1-RMD-1037/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Payments/PaymentAggregate/IPaymentRepository.cs
  - src/Modules/Payments/PaymentAggregate/PostgresPaymentRepository.cs
  - src/Modules/Billing/PaymentClosure/BillPaymentClosureProjection.cs
  - src/Modules/Billing/PaymentClosure/BillPaymentClosureProjector.cs
  - tests/Host/Experience/Billing/PaymentRefundOpenRequestClosureHttpTests.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-open-refund-closed.test.js
  - tests/Clients/StaticApps/split-payment-item-refund.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar; AwaitingManualRefund durumundaki (onaylanmış) istekler

## Dependencies

- V1-RMD-1036

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
