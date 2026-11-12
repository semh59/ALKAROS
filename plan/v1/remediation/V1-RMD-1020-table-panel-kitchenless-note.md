# V1-RMD-1020 - Masa panelinde mutfaksız eklenen kalem işaretlenir

- Task ID: V1-RMD-1020
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa panelindeki kalem listesinde kasada mutfağa gitmeden eklenen kalem hiçbir not taşımıyordu; ödeme, bölme ve Bekleyen hesaplar ekranlarında not vardı. Masa paneli kalem satırı mutfağa gitmeden eklenen kalemin yanına kasada eklendi notunu yazar.

## Owned surface

- `evidence/V1-RMD-1020/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/contracts.ts
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1019

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
