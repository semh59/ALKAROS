# V1-RMD-1014 - Açık hesaplı dolu masada Müsait yap gösterilmez

- Task ID: V1-RMD-1014
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Açık siparişi veya hesabı olan dolu masada Müsait yap işlemi masa kartında, masa panelindeki Diğer işlemler menüsünde ve plan görünümünde sunuluyordu; basılırsa masa boş görünür, açık hesap sahipsiz kalır. Açık sipariş veya hesabı olan masada bu işlem hiçbir yerde sunulmaz; boş dolu masada (hesapsız) sunulmaya devam eder.

## Owned surface

- `evidence/V1-RMD-1014/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/tables/models.ts
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.tsx
  - src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1013

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
