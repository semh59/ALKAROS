# V1-RMD-224 - Performans raporu sorgusuna eksik indeks eklendi

- Task ID: V1-RMD-224
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **MEDIUM** bulgu:
`PostgresKitchenTicketRepository.GetCompletedTicketTimingsAsync`
(V1-KIT-014'ün performans raporu sorgusu) `WHERE created_at >=
@window_start AND created_at < @window_end AND ready_at IS NOT NULL
ORDER BY created_at` çalıştırıyor — `kitchen.kitchen_tickets`'ın
mevcut iki indeksinden (`order_id`; `station_id, status`) hiçbiri bu
aralık+null-olmayan filtresini desteklemiyor. Restoran ay/yıl
boyunca ticket biriktirdikçe, geniş bir tarih aralığı için rapor
çeken bir yönetici sorguyu tam tablo taramasına zorluyordu.

## Owned surface

- `database/migrations/V1/V1-RMD-224/**` (yeni)

Sınırlı ek (yollar geri-tik olmadan):

- database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs (paylaşılan) —
  yeni migration pozisyonu 114 kaydedildi, `PhaseBMax` güncellendi.
- tests/Host/MigrationComposition/Manifest/ManifestTests.cs (ilgili
  test projesi) — sabit sayı/id listesi 114'ü yansıtacak şekilde
  güncellendi.
- tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  (ilgili modül) — indeksin gerçekten var olduğunu doğrulayan yeni
  test.

## In scope

1. Yeni kısmi indeks: `ix_kitchen_tickets_completed_timing ON
   kitchen.kitchen_tickets (created_at) WHERE ready_at IS NOT NULL` —
   `ix_orders_pending_confirmation`'ın (V1-RMD-156) aynı "yalnız
   eşleşebilecek satırları indeksle" ilkesiyle.
2. Yeni test: `pg_indexes`'e karşı indeksin varlığını doğruluyor.

## Out of scope

- Sorgunun kendisi — zaten doğru, yalnız destekleyici indeks eksikti.

## Dependencies

- V1-KIT-014
- V1-RMD-156

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Host/Experience/KitchenOperations` →
  35/35 yeşil (yeni test dahil); revert-and-confirm ile (indeks
  oluşturma geçici olarak devre dışı bırakılıp) testin gerçekten
  kırıldığı kanıtlandı.
- `tests/Host/MigrationComposition`'ın `ManifestTests` alt kümesi →
  16/16 yeşil (yeni migration pozisyonu doğrulandı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
