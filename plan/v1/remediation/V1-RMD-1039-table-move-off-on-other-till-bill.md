# V1-RMD-1039 - Başka kasada açık hesabı olan masada Masa taşı pasif olur

- Task ID: V1-RMD-1039
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa panelinde başka bir kasanın açık hesabı olan masada Öde, Böl ve İptal / iade pasifken Masa taşı etkin kalıyordu. Masa taşı da aynı koşulla (billOnOtherTerminal) pasif olur; neden zaten panelde yazılı ("Bu hesap başka bir kasada açık..."). PO talebi: kasiyer denemesi 6, sorun 5.

## Owned surface

- `evidence/V1-RMD-1039/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.tsx
  - src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1038

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
