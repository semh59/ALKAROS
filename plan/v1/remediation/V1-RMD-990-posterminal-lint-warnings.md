# V1-RMD-990 - PosTerminal lint uyarıları sıfırlanır

- Task ID: V1-RMD-990
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

oxlint çıktısındaki 12 uyarı tek tek giderilir; pnpm build sıfır uyarıyla geçer.

## Owned surface

- `evidence/V1-RMD-990/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/focusOnMount.ts
  - src/Clients/PosTerminal/src/routes/ReservationStation.tsx
  - src/Clients/PosTerminal/src/routes/RelaySettings.tsx
  - src/Clients/PosTerminal/src/routes/QnbCredentialSettings.tsx
  - src/Clients/PosTerminal/src/routes/CustomerDisplayScreensaverSettings.tsx
  - src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.tsx
  - src/Clients/PosTerminal/src/routes/SecurityAdministration.tsx
  - src/Clients/PosTerminal/src/routes/TokenTerminalSettings.tsx
  - src/Clients/PosTerminal/src/design-system/primitives.tsx
  - src/Clients/PosTerminal/src/router.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.logout-clears-storage.test.tsx
  - src/Clients/PosTerminal/src/routes/loginFocus.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-989

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
