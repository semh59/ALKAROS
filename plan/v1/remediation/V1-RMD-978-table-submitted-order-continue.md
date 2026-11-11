# V1-RMD-978 - Gonderilmis masa siparisine devam edilir

- Task ID: V1-RMD-978
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masada gönderilmiş (mutfağa gitmiş) bir sipariş varken kasiyer "Siparişe devam et" dediğinde 409 hatası almaz: aynı hesaba yeni bir tur eklenir, önceki kalemler değişmez, yalnız gönderilmemiş kalemler düzenlenebilir.

## Owned surface

- `evidence/V1-RMD-978/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenStore.cs
  - src/Host/DualScreen/DualScreenStore.Orders.cs
  - src/Host/DualScreen/DualScreenStore.Display.cs
  - src/Modules/Orders/OrderAggregate/Order.cs
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
  - tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs
  - src/Host/DualScreen/DualScreenContracts.cs
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs
  - src/Clients/PosTerminal/src/contracts.ts
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.first-touch.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-977

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
