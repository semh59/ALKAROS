# V1-RMD-1113 - Mutfak masa kartı yüksekliği sınırlıdır

- Task ID: V1-RMD-1113
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasa kullanıcı denemesi 12: açık siparişte bile çok biletli masa kartı sınırsız uzuyor (M-10 1319 px, M-03 1085 px). Masa kartının istasyon bölümü makul bir en büyük yükseklikle sınırlanır ve taşan biletler kart içinde dikey kayar; kart başlığı (masa, süre, uyarı düğmesi) görünür kalır. 14 px yazı ve 48 px dokunma kuralları değişmez.

## Owned surface

- `evidence/V1-RMD-1113/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchen-operations.css
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchenCardHeight.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Bilet süzgeci ve sunucu kodu; kart düzeni ve renkleri

## Dependencies

- V1-RMD-1112

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
