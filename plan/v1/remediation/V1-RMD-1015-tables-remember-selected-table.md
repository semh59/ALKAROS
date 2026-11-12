# V1-RMD-1015 - Masalar ekranına dönünce son seçilen masa seçili kalır

- Task ID: V1-RMD-1015
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer bir masada sipariş verip Masalar ekranına dönünce seçili masa ilk masaya (M-01) sıfırlanıyordu; yanlış masaya işlem yapma riski doğuruyordu. Son seçilen masa tarayıcıda hatırlanır ve ekrana dönüşte yeniden seçilir; masa artık listede yoksa ilk masa seçilir.

## Owned surface

- `evidence/V1-RMD-1015/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1014

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
