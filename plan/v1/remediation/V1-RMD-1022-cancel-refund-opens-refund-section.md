# V1-RMD-1022 - İptal / iade düğmesi iade bölümü açık ekrana gider

- Task ID: V1-RMD-1022
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa panelinde Böl ve İptal / iade düğmeleri aynı hesap bölme ekranını açıyordu. Böl hesap bölme ekranına gider; İptal / iade ödeme ekranını iade ve iptal bölümü açık olarak açar (ödeme ekranındaki open=refund işareti).

## Owned surface

- `evidence/V1-RMD-1022/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-open-refund-fold.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1021

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
