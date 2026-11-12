# V1-RMD-1004 - Kasiyer masa planında yapılandırma düğmeleri yalnız yetkiliye görünür

- Task ID: V1-RMD-1004
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer rolü masa planında Bölge ekle, Masa ekle, Masayı düzenle, Bölgeleri yönet ve Planı düzenle düğmelerini görmez; bu düğmeler yalnız floorplan.manage yetkisi olana görünür (kasiyerin masa üzerindeki işlem yetkileri etkilenmez).

## Owned surface

- `evidence/V1-RMD-1004/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1001

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
