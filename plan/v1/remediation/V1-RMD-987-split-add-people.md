# V1-RMD-987 - Hesap bölmede kişiler önceden kayıtlı olmak zorunda değil

- Task ID: V1-RMD-987
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer hesap bölme ekranında Kişi ekle düğmesiyle kişi ekler ve ürün, tutar veya eşit bölmeyi bu kişilere yapar; masaya sandalye veya kişi önceden kaydedilmesi gerekmez.

## Owned surface

- `evidence/V1-RMD-987/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.recovery.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-986

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
