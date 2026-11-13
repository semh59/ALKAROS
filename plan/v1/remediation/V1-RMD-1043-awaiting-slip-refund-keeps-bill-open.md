# V1-RMD-1043 - Onaylanmış ama fişi girilmemiş kart/havale iadesi olan hesap "Ödendi" olmaz; ekran nedenini Türkçe söyler

- Task ID: V1-RMD-1043
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1037'deki "açık iade isteği" kapanış engeli (HasOpenRefundRequest) yalnız onay bekleyen (Requested) istekleri sayıyordu. Kart/havale iadesi onaylanıp fiş girilmeden (AwaitingManualRefund) hesap "Ödendi" olabiliyor, iade tamamlanınca ödeme düşüp hesap eksik kalıyordu. Aynı engel, aynı depo sorgusu ve aynı kapanış yoluyla AwaitingManualRefund durumunu da kapsar: fiş girilene (Completed) ya da istek reddedilene kadar hesap açık kalır. Ödeme ekranı bu durumda "Tahsilat Tamam, Hesap Açık" ve iadenin onaylandığını, fiş/havale bilgisi girilene kadar hesabın kapanmayacağını söyler. Semih kararı 2026-11-13 (kasiyer denemesi 6, sorun 4'ün devamı).

## Owned surface

- `evidence/V1-RMD-1043/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Payments/PaymentAggregate/IPaymentRepository.cs
  - src/Modules/Payments/PaymentAggregate/PostgresPaymentRepository.cs
  - src/Modules/Billing/PaymentClosure/BillPaymentClosureProjection.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Host/Experience/Billing/PaymentRefundOpenRequestClosureHttpTests.cs
  - tests/Clients/StaticApps/split-payment-open-refund-closed.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1042

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
