# V1-RMD-953 - Yetki kararları ekranı iade onayının nerede olduğunu söylesin

- Task ID: V1-RMD-953
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: yönetici iade onayını Yetki kararları ekranında arıyor ve bulamıyor; iade istekleri Hesap ekranındaki iade panelinde karara bağlanıyor. Karar: Yetki kararları ekranı başlığın altında tek cümleyle iade isteklerinin Hesap ekranından onaylandığını söyler.

## Owned surface

- `evidence/V1-RMD-953/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/authorization-decisions/models.ts
  - src/Clients/PosTerminal/src/features/authorization-decisions/AuthorizationDecisionsWorkspace.tsx
  - src/Clients/PosTerminal/src/features/authorization-decisions/AuthorizationDecisionsWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-952

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
