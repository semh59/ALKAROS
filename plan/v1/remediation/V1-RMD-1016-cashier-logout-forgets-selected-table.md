# V1-RMD-1016 - Satış ekranındaki Çıkış hatırlanan masayı da siler

- Task ID: V1-RMD-1016
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Satış ekranından çıkış yapılınca hatırlanan son masa (alkaros.selected-table) silinmiyordu; sonraki kasiyer Masalar ekranını önceki kasiyerin masası seçili açıyordu. Çıkış, yarım ödeme ekranı anahtarıyla birlikte bu anahtarı da siler.

## Owned surface

- `evidence/V1-RMD-1016/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.logout-clears-storage.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1015

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
