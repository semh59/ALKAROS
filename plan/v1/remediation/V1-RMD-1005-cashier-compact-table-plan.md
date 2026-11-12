# V1-RMD-1005 - Kasiyer masa planı tek satırlık araç çubuğu ve kartlarla taslağa uyar

- Task ID: V1-RMD-1005
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyerin masa planı ekranı onaylanan taslağa uyar: başlık, filtre ve sayaç blokları tek araç çubuğu satırında toplanır (bölgeler, masa arama, Hızlı satış, Yenile), durum filtreleri ve sayaç kartları yalnız yapılandırma yetkisi olana görünür, kartlarda Masayı aç ve Müsait yap gibi düğmeler yoktur, karta dokunmak sağ paneli açar; 1024x768 ekranda ilk iki masa sırası görünür.

## Owned surface

- `evidence/V1-RMD-1005/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/tables/tables.css
  - src/Clients/PosTerminal/src/features/tables/tablesStyle.test.ts
  - src/Clients/PosTerminal/src/features/tables/tablePlanModel.ts
  - src/Clients/PosTerminal/src/features/tables/tablePlanModel.test.ts
  - src/Clients/PosTerminal/src/features/tables/models.ts
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/shell/shell.css
  - src/Clients/PosTerminal/src/shell/ProductionShell.tsx
  - src/Clients/PosTerminal/src/shell/ProductionShell.test.tsx
  - src/Clients/PosTerminal/src/styles.css
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1004

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
