# V1-RMD-1072 - Genel birincil düğme camgöbeği zeminde ink yazı kullanır

- Task ID: V1-RMD-1072
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

primitives.css'teki .ds-button--primary beyaz yazıyı camgöbeği (#00cfff) zeminde kullanıyor; kontrast 1,85:1 ve docs/CASHIER_UI_RULES.md 5 accent üzerinde yazının daima ink olmasını ister. Düğme bileşeninin varsayılan varyantı olduğu için bütün Button kullanımları etkilenir; kural gereği yazı ink olur ve cashierUiRules.test.ts bunu korur.

## Owned surface

- `evidence/V1-RMD-1072/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/design-system/primitives.css
  - src/Clients/PosTerminal/src/design-system/cashierUiRules.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1071

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
