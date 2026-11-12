# V1-RMD-993 - Boş taslak masa siparişini engellemez

- Task ID: V1-RMD-993
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasada ürünü kalmamış, masasız boş bir taslak varken Masada sipariş aç hata vermez; boş taslak sunucuda iptal edilir ve masa siparişi açılır; ürünü olan taslak yine uyarır.

## Owned surface

- `evidence/V1-RMD-993/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenStore.Orders.cs
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-992

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
