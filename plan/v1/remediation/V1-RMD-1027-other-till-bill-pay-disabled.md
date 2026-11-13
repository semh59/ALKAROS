# V1-RMD-1027 - Başka kasanın hesabında masa panelinde Öde önceden pasif

- Task ID: V1-RMD-1027
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa planında başka bir kasanın açtığı hesap için Öde, Böl ve İptal / iade etkin görünüyor; basınca 403 geliyor ve kasiyer iki dokunuş sonra nedenini öğreniyor. Masa listesi yanıtına billOnOtherTerminal alanı eklenir (cash.session.override yetkisi olmayan kullanıcı için, hesap başka terminale aitse); masa paneli bu üç düğmeyi pasif yapar ve nedeni yazar.

## Owned surface

- `evidence/V1-RMD-1027/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Tables/TableManagementStore.cs
  - src/Host/Experience/Tables/TableManagementApplication.cs
  - src/Host/Experience/Tables/TableManagementContracts.cs
  - tests/Host/Experience/Tables/TableManagementHttpTests.cs
  - src/Clients/PosTerminal/src/features/tables/models.ts
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1026

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
