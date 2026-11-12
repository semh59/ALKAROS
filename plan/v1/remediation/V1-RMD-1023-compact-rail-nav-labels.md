# V1-RMD-1023 - Dar ekranda yan menü etiketleri görünür

- Task ID: V1-RMD-1023
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

768-1279 piksel arası ekranlarda (1024x768 dahil) yan menü yalnız ikon gösteriyor, etiketler gizliydi; kasiyer Masalar, Bekleyen hesaplar ve Diğer'i ikondan ayırt edemiyordu. Dar yan şeritte etiket ikonun altında küçük yazıyla görünür; rozet köşede kalır.

## Owned surface

- `evidence/V1-RMD-1023/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/shell/shell.css
  - src/Clients/PosTerminal/src/shell/shellStyle.test.ts
  - src/Clients/PosTerminal/src/shell/ProductionShell.tsx
  - src/Clients/PosTerminal/src/shell/ProductionShell.test.tsx
  - src/Clients/PosTerminal/src/design-system/tokens.css
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1022

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
