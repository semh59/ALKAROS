# V1-RMD-957 - Kasiyer Masalar ekranında işlemeyen sipariş düğmesini görmesin

- Task ID: V1-RMD-957
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: Masalar ekranında kasiyere yönetici işlemi olan 'Masada sipariş aç' düğmesi soluk ve basılamaz görünüyor. Karar: sipariş açma yetkisi olmayan kullanıcıya düğme hiç çizilmez; yalnız Hesabı al kalır, açıklama metni de buna uyar.

## Owned surface

- `evidence/V1-RMD-957/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-956

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
