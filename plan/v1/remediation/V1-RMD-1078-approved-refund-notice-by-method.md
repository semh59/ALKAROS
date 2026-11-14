# V1-RMD-1078 - Kapanmış hesapta onaylanmış iade metni iade yöntemine göre

- Task ID: V1-RMD-1078
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1077 kapanmış hesapta onaylanmış iade için "İade onaylandı, fiş girişi bekleniyor." yazar; havale iadesinde fiş yoktur. PO kararı: iade yöntemi kart ise "İade onaylandı, fiş girişi bekleniyor.", havale (Eft) ise "İade onaylandı, iade dekontu girişi bekleniyor." yazılır. Yöntem istek satırının `method` alanından okunur; okunamazsa mevcut cümle kalır. Ek olarak açık hesapta onay bekleyen ve onaylanmış iade birlikteyken metnin yönetici kararını öne aldığı test edilir.

## Owned surface

- `evidence/V1-RMD-1078/**`
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

- V1-RMD-1077

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
