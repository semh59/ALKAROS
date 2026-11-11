# V1-RMD-981 - Test veritabani sablon kopyasi ile hizlandirma

- Task ID: V1-RMD-981
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

DualScreenStoreTests her testte veritabanını sıfırdan taşıyarak (migration) çalıştığı için sınıf 8,5 dakika sürüyor ve test koşucusu kilitlenmiş gibi görünüyor; veritabanı bir kez taşınıp şablondan kopyalanır, sınıf dakikalar yerine saniyelerde biter ve test sonuçları değişmez.

## Owned surface

- `evidence/V1-RMD-981/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/MigrationComposition/Fixtures/TestDatabase.cs
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-980

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
