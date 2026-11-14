# V1-RMD-1077 - Kapanmış hesapta onaylanmış iade "hesap kapanmaz" demez

- Task ID: V1-RMD-1077
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1076 kapanmış hesapta onay bekleyen iade isteği için "hesap kapanmaz" cümlesini düzeltir. Aynı yanlış cümle onaylanmış ama fiş veya havale adımı bekleyen istek (AwaitingManualRefund) için de kapanmış hesapta görünür: "Bu yapılana kadar hesap kapanmaz." Kapanmış hesapta bu istek için hesap açık denmez; kasiyere yalnız iade adımının bekleyen işlem olduğu söylenir. Metni PO belirler.

## Owned surface

- `evidence/V1-RMD-1077/**`
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

- V1-RMD-1076

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
