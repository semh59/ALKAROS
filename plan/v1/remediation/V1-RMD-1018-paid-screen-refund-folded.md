# V1-RMD-1018 - Ödeme sonucu ekranında iade bölümü kapalı gelir

- Task ID: V1-RMD-1018
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Hesap ödendi ekranında iade formları (iade adedi kutuları, seçim kutuları, iade iste) açık kart olarak başarı mesajının hemen altında duruyor ve ekranı kalabalıklaştırıyordu. İade bölümü kapalı bir İade başlığı altına alınır; bekleyen iade isteği, iade hatası veya iade bildirimi varsa açık gelir.

## Owned surface

- `evidence/V1-RMD-1018/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-paid-refund-fold.test.js
  - tests/Clients/StaticApps/split-payment-refunds.test.js
  - tests/Clients/StaticApps/split-payment-item-refund.test.js
  - tests/Clients/StaticApps/split-payment-allowance.test.js
  - tests/Clients/StaticApps/split-payment-comped-close.test.js
  - tests/Clients/StaticApps/split-payment-cancel-order.test.js
  - tests/Clients/StaticApps/split-payment-receipt-reset.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1017

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
