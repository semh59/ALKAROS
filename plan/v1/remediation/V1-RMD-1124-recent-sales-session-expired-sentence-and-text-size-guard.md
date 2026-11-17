# V1-RMD-1124 - Son satışlar: oturum süresi cümlesi ve yazı boyutu kapısı

- Task ID: V1-RMD-1124
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 15: Son satışlar listesi (V1-RMD-1117/1118) oturum 401 ile sona erince genel "okunamadı" cümlesi gösteriyor; ayrı bir cümle ("Oturum süresi doldu, yeniden giriş yapın.") gösterir. Ayrıca recent-sales.css, kasiyer ekranı yazı boyutu kapısına (cashierTextSize.test.ts, 14 px altı yasak) eklenir. Sunucu sözleşmesi ve liste davranışı değişmez.

## Owned surface

- `evidence/V1-RMD-1124/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/recent-sales/recentSalesApi.ts
  - src/Clients/PosTerminal/src/features/recent-sales/RecentSalesWorkspace.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/cashierTextSize.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci typecheck, testler ve üretim derlemesi (client-build-gate)

## Out of scope

- Sunucu sözleşmesi; Son satışlar ekranının diğer davranışı; diğer CSS dosyaları

## Dependencies

- V1-RMD-1120

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. client-build-gate çıktısı exit code 0

## Handoff

- None
