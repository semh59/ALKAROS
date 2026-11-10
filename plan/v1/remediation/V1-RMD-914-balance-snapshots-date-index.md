# V1-RMD-914 - Bakiye kayıt tablosuna tarih indeksi (migration 224)

- Task ID: V1-RMD-914
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-896/910 açığı (koordinatör kararı: ertelenmez). Bakım işi her 6 saatte snapshot_date üzerinden (eski kayıt silme, mevcut günleri okuma) tabloyu tarıyor; tabloda yalnız (customer_id, snapshot_date) indeksi var, tarih tek başına aranınca tam tarama oluyor. Karar: customer_account.balance_snapshots (snapshot_date) indeksi, geri alınabilir migration 224.

## Owned surface

- `evidence/V1-RMD-914/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - database/migrations/V1/V1-RMD-914/224-balance-snapshots-date-index.up.sql
  - database/migrations/V1/V1-RMD-914/224-balance-snapshots-date-index.down.sql
  - src/Host/Composition/Migrations/MigrationManifest.cs
  - database/MigrationComposition/order.json
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Test: indeks var; geri alma indeksi kaldırır; yeniden uygulama çalışır
2. Migration ve kayıt değişiklikleri
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Sorgu veya iş davranışı değişmez

## Dependencies

- V1-RMD-913

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
