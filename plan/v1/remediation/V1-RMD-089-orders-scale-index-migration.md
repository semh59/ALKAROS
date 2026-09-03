# V1-RMD-089 - Orders scale index migration

- Task ID: V1-RMD-089
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-01
- PDF:I.38
- PDF:I.45.1
- EXT:POSTGRESQL-18.4

## Goal

1M sipariş + 3M kalem satırı ölçek probunda `orders.orders` üzerinde iki sorgu deseni tam tablo (parallel) seq scan yapıyor: (1) tarih aralıklı raporlama toplamları — bir iş günü cirosu 134 ms; (2) masaya ait en güncel açık sipariş — `table_id` + durum filtresi 158 ms; ve geçmiş biriktikçe lineer bozuluyor. Bu görev bu iki deseni indeksleyen bir ileri/geri migration ekler ve öncesi/sonrası `EXPLAIN ANALYZE` ile kanıtlar.

## Owned surface

- `plan/v1/remediation/V1-RMD-089-orders-scale-index-migration.md`
- `database/migrations/V1/V1-RMD-089/**`
- `evidence/V1-RMD-089/**`
- src/Host/Composition/Migrations/MigrationManifest.cs içindeki PhaseBMax sabiti V1-FND-004 sahipliğinde kalır; 9. dalgadaki gibi bu dalgada yalnızca faz üst sınırı 041 değerine güncellenir.
- PO:2026-09-04 kararıyla database/MigrationComposition/order.json ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs yüzeyleri, migration 043 ekleyen V1-IAM-017'ye devredildi; bu historical task closed kalır.

## In scope

- `database/migrations/V1/V1-RMD-089/041-orders-scale-indexes.up.sql` ve `.down.sql`: `orders.orders (created_at)` btree indeksi ve açık sipariş durumlarıyla filtreli kısmi indeks `(table_id, created_at DESC) WHERE status IN (açık durumlar)`.
- `database/MigrationComposition/order.json` içine `041` girişi (`phase B`, `tables ["orders"]`).
- `src/Host/Composition/Migrations/MigrationManifest.cs` içinde `PhaseBMax` değerini `041` yapmak.
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs` içinde çalışma zamanı manifest kimlik listesi, sayım ve son giriş tabloları beklentisini güncellemek.
- 1M satır seed edilmiş şema klonu üzerinde her iki indeks için öncesi/sonrası `EXPLAIN ANALYZE` kanıtı.

## Out of scope

- `orders` dışındaki tablolara indeks; ölçek probunda açık bulunmamıştır.
- Sorgu veya repository kodunu değiştirmek; indeksler mevcut sorgu planlarını iyileştirir.
- Veri saklama / purge işleri.

## Dependencies

- V1-GOV-054

## Deliverables

- `database/migrations/V1/V1-RMD-089/041-orders-scale-indexes.up.sql` ve `.down.sql`.
- Güncellenmiş `order.json`, `MigrationManifest.cs` ve `ManifestTests.cs`.
- `evidence/V1-RMD-089/` altında öncesi/sonrası `EXPLAIN ANALYZE` çıktısı ve migration ileri/geri kanıtı.

## Acceptance evidence

- Migration atılabilir PostgreSQL 18 üzerinde ileri ve geri sorunsuz çalışır; `\d orders.orders` iki yeni indeksi gösterir.
- 1M satır üzerinde: iş günü cirosu sorgusu seq scan (~134 ms) yerine bitmap/index scan (~14 ms); masaya ait açık sipariş sorgusu seq scan (~158 ms) yerine index scan (< 1 ms).
- `dotnet test` migration composition testleri (`ManifestTests`) sıfır hata verir.

## Handoff

- V1-GOV-055
