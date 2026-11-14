# V1-RMD-1069 - Masa panelindeki durum rozeti kartla aynı yazı rengini alır

- Task ID: V1-RMD-1069
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 8: Ödeme bekliyor ve Dolu rozetlerinin zemini kartla panelde aynı, ama yazı rengi panelde gri. Neden: tables.css'teki .table-details__header > div > span:last-child kuralı durum rozetinin bulunduğu kapsayıcıdaki son span'e de uygulanıp rozetin rengini ezer. Kural yalnız başlığın bağlam satırına uygulanır; docs/CASHIER_UI_RULES.md 4 gereği rozet her ekranda aynı yazı jetonunu kullanır; tablesStyle.test.ts bunu korur.

## Owned surface

- `evidence/V1-RMD-1069/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/tables/tables.css
  - src/Clients/PosTerminal/src/features/tables/tablesStyle.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1068

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
