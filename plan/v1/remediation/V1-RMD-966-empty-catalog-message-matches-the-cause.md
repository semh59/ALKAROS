# V1-RMD-966 - Boş katalog mesajı arama yokken aramadan bahsetmesin

- Task ID: V1-RMD-966
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: katalogda hiç ürün yokken ürün alanı 'Aramanızla eşleşen aktif ürün bulunamadı.' diyor; kimse arama yapmadı. Karar: arama yazılmamışsa 'Katalogda satılabilir ürün yok. Ürünleri yönetici Menü ekranından ekler.', arama yazılmışsa mevcut mesaj.

## Owned surface

- `evidence/V1-RMD-966/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.first-touch.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-965

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
