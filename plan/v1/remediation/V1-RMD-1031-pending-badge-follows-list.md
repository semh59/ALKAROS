# V1-RMD-1031 - Bekleyen hesaplar rozeti açık liste ile aynı sayıyı gösterir

- Task ID: V1-RMD-1031
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Bekleyen hesaplar ekranındayken menüdeki rozet güncellenmiyordu (yoklama bu ekranda kapalıydı) ve ekrandan çıkana kadar listeyle tutarsız kalıyordu. Liste her yüklendiğinde sayısını üst menüye bildirir; rozet o ekranda listenin sayısını gösterir, diğer ekranlarda 20 saniyelik yoklamayı kullanır.

## Owned surface

- `evidence/V1-RMD-1031/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1030

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
