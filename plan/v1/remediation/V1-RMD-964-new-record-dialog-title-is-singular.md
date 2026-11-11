# V1-RMD-964 - Yeni kayıt penceresi başlığı tekil olsun

- Task ID: V1-RMD-964
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: Vergi profili eklerken pencere başlığı 'Yeni vergi profilleri' (çoğul) yazıyor; başlık sekme adının sonundaki ler/lar atılarak türetiliyor ve -leri/-ları ile bitenlerde çalışmıyor. Karar: başlık zaten var olan tekil etiket tablosundan (catalogAddLabels) üretilir.

## Owned surface

- `evidence/V1-RMD-964/**`
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

- V1-RMD-963

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
