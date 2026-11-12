# V1-RMD-1017 - Oturum süresi dolunca hatırlanan masa ve hesap silinir

- Task ID: V1-RMD-1017
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Oturum 401 ile sona erince (süre dolması) hatırlanan son masa ve yarım ödeme ekranı hesabı silinmiyordu; sonraki kasiyer önceki kasiyerin masası ve hesabıyla açıyordu. Oturum sona erdiğinde de çıkıştaki gibi bu iki anahtar silinir.

## Owned surface

- `evidence/V1-RMD-1017/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.session-ended-message.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1016

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
