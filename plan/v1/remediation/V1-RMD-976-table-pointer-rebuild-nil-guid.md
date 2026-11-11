# V1-RMD-976 - Masa işaretçisi onarımı boş (sıfır) kimlik yazmasın

- Task ID: V1-RMD-976
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa işaretçisi onarımı, açık siparişi olup hesabı olmayan masaya hesap kimliği olarak sıfır GUID (00000000-...) yazıyor ve bu yüzden Siparişe devam et 409 veriyor, Hesabı al boş hesap numarasıyla açılıyor. Onarım yalnız gerçek kimlik ya da NULL yazmalı; veritabanında kalmış sıfır GUID işaretçileri temizlenmeli ve bir daha yazılamamalı.

## Owned surface

- `evidence/V1-RMD-976/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Tables/CurrentPointers/PostgresTablePointerProjector.cs
  - tests/Modules/Tables/CurrentPointers/PostgresTablePointerProjectorTests.cs
  - database/migrations/V1/V1-RMD-976/228-table-pointers-no-nil-guid.up.sql
  - database/migrations/V1/V1-RMD-976/228-table-pointers-no-nil-guid.down.sql
  - database/MigrationComposition/order.json
  - src/Host/Composition/Migrations/MigrationManifest.cs
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-975

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
