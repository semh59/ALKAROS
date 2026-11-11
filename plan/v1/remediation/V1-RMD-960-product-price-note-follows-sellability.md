# V1-RMD-960 - Fiyatlar bölümü, ürün kaydındaki geçerli fiyatı varken 'satılabilir değil' demesin

- Task ID: V1-RMD-960
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: ürün formundaki Satış fiyatı ürünü satılabilir yapıyor (kasa satışı ürün kaydındaki current_price'a bakar; yalnız tarihli fiyat bir kez başladıysa onun sürümü geçerli olur), ama ürün ayrıntısındaki Fiyatlar bölümü tarihli fiyat kaydı yoksa 'Geçerli fiyat yok; satılabilir değil.' diyor. Karar: ürün kaydında fiyat varsa bölüm bunu söyler ('Ürün kaydındaki fiyat geçerli: X; tarihli fiyat kaydı yok'), 'satılabilir değil' yalnız ürün kaydında da fiyat yoksa çıkar.

## Owned surface

- `evidence/V1-RMD-960/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-959

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
