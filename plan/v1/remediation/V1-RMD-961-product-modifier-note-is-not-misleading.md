# V1-RMD-961 - Ürün ayrıntısı, grup bağlıyken 'modifikatör atanmadı' demesin

- Task ID: V1-RMD-961
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: bir ürüne modifikatör grubu bağlıyken ayrıntıda 'Bu ürüne modifikatör atanmadı.' yazıyor. Neden: ilk bölüm yalnız doğrudan o ürüne ait (product_id'li) modifikatörleri listeler, grup bağlantıları ikinci bölümde. Karar: ilk bölüm 'ÜRÜNE ÖZEL MODİFİKATÖRLER' / 'Bu ürüne özel modifikatör yok.' olur; grup bağlantısı bölümü aynen kalır.

## Owned surface

- `evidence/V1-RMD-961/**`
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

- V1-RMD-960

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
