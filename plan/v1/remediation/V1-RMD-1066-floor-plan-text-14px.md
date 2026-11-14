# V1-RMD-1066 - Salon planı arayüzünde masa düğümü dışındaki yazılar 14 piksele çıkarılır, düğüm içi satırların gerekçesi yazılır

- Task ID: V1-RMD-1066
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

floorPlan.css araç çubuğu, inspektör, inceleme ve liste yazıları 11,2-12,5px. Kasiyer kuralı 14px; bunlar sığma kısıtı olmayan yazılardır ve 14px'e çıkarılır, masa numarası da 14px alt sınırı alır. Masa düğümü içindeki ikincil satırlar (durum, bağlam, geçen süre, rozet, koltuk numarası) tuvaldeki düğüm geometrisine bağlıdır; 14px'e çıkarlarsa düğümler taşar, bu yüzden adı konmuş istisna olarak kalır ve gerekçesi test dosyasında yazılır. tablesStyle.test.ts bu sınırı korur.

## Owned surface

- `evidence/V1-RMD-1066/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/tables/floorPlan.css
  - src/Clients/PosTerminal/src/features/tables/tablesStyle.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar; masa düğümü geometrisi ve düzenleyici davranışı

## Dependencies

- V1-RMD-1065

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
