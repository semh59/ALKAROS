# V1-RMD-994 - Masa siparişi hızlı satış sepetine karışmaz ve sepet masa adını gösterir

- Task ID: V1-RMD-994
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masalar ekranından Siparişe devam et ile açılmamış bir masa siparişi ana kasa sepetinde görünmez ve hızlı satış ürünü o masaya yazılmaz; masa siparişi açıkken sepet başlığı masa adını gösterir.

## Owned surface

- `evidence/V1-RMD-994/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenContracts.cs
  - src/Host/DualScreen/DualScreenStore.Display.cs
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
  - src/Clients/PosTerminal/src/contracts.ts
  - src/Clients/PosTerminal/src/tableCart.ts
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.table-cart.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-993

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
