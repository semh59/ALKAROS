# V1-RMD-1079 - Kapanmış hesapta birden çok bekleyen iade metinde görünür

- Task ID: V1-RMD-1079
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kapanmış hesapta hem onay bekleyen hem onaylanmış iade varken başlık altında yalnız onaylanmış iade cümlesi görünür (V1-RMD-1077 kabul edilen riski). Kasiyere iki bekleyen işlemin de var olduğu söylenir. Metni PO belirler.

## Owned surface

- `evidence/V1-RMD-1079/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-open-refund-closed.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1078

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
