# V1-RMD-904 - Tam regresyon koşusu: Host.Tests parçalı, PosTerminal vitest ve tsc, StaticApps vitest

- Task ID: V1-RMD-904
- Status: Planned
- Assignee: Unassigned
- Work type: validation
- Surface state: Existing

## Goal

V1-RMD-891..903 değişikliklerinden sonra bütün testlerin birlikte yeşil olduğunu kanıtlar: Host.Tests proje proje parçalı, PosTerminal vitest ve tsc, StaticApps vitest. Kırmızılar araştırılır; değişikliğe bağlıysa düzeltilir (ayrı plan değişikliğiyle), önceden varsa kanıtla ayrıştırılır.

## Owned surface

- `evidence/V1-RMD-904/**`

- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Parçalı Host.Tests, PosTerminal vitest ve tsc, StaticApps vitest, log ve özet
2. İki bağımsız qa incelemesi

## Out of scope

- Üretim ve test kodu değişikliği (kırmızı çıkarsa ayrı plan değişikliği)

## Dependencies

- V1-RMD-905

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
