# V1-RMD-1040 - Masa taşıma tek terimle (Masa taşı) anılır; kasiyer giriş başlığı kasa açma ile karışmaz

- Task ID: V1-RMD-1040
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa taşıma işlemi düğmede "Masa taşı", pencere başlığında ve bildirimde "Masa değiştir" diye anılıyordu; kasiyer arayüzünde terim "Masa taşı" olur (düğme, pencere başlığı, "Masa taşı tamamlandı." bildirimi, salon planı düğmesi). Kasiyer giriş ekranının başlığı "Kasayı aç" kasa oturumu açmayla karıştığı için "Giriş yap" olur. Garson uygulamasının kendi metni bu görevin dışındadır. PO talebi: kasiyer denemesi 6, sorun 6.

## Owned surface

- `evidence/V1-RMD-1040/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.login-focus.test.tsx
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.test.tsx
  - tests/E2E/Cashier/specs/11-cross-client-payment-aware-transfer.spec.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1039

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
