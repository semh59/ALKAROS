# V11-INV-009 - StockItem.ReorderPoint

- Task ID: V11-INV-009
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Planned

## Goal

Düşük stok uyarısı zincirinin son eksik parçası: `StockItem`'de kalıcı bir
eşik alanı yok; `CriticalStockReport` var ama eşik her çağrıda çağıranın
verdiği bir parametre (varsayılan 0), kalıcı değil. Bu görev, her stok
kalemine kalıcı, opsiyonel bir `ReorderPoint` ekliyor — null geldiğinde
mevcut davranış birebir korunuyor, hiçbir mevcut çağrı bozulmuyor.

## Owned surface

- `database/migrations/V11/V11-INV-009/**` (yeni)

Sınırlı ek (yollar geri-tik olmadan, StockMaster'ın kendisi V11-INV-004'ün
sahipliğinde — bu görev yalnızca mevcut dosyalara alan/parametre ekliyor):

- src/Modules/Inventory/StockMaster/StockItem.cs,
  src/Modules/Inventory/StockMaster/IStockItemRepository.cs,
  src/Modules/Inventory/StockMaster/PostgresStockItemRepository.cs,
  src/Modules/Inventory/StockMaster/IStockMasterService.cs,
  src/Modules/Inventory/StockMaster/StockMasterService.cs,
  tests/Modules/Inventory/StockMaster/** (paylaşılan, V11-INV-004
  sahipliğinde) — `ReorderPoint` alanı + repository/servis desteği.
- src/Modules/Reporting/MenuInventory/PostgresMenuInventoryReportingService.cs
  (paylaşılan) — `CriticalStockReportItem.CriticalThreshold` artık
  `item.ReorderPoint ?? query.CriticalThreshold ?? 0` olarak hesaplanıyor.
- src/Host/Experience/Inventory/StockMasterEndpoints.cs,
  src/Host/Experience/Inventory/StockMasterContracts.cs (paylaşılan) — yeni
  uç: `PUT /api/v1/management/inventory/stock-items/{id}/reorder-point`;
  `CreateStockItemV1`'e `ReorderPoint` alanı eklendi.
- tests/Host/Experience/Inventory/StockMasterHttpTests.cs,
  tests/Modules/Reporting/MenuInventory/MenuInventoryReportingDatabaseTests.cs
  (paylaşılan) — yeni testler.
- database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs (paylaşılan) —
  migration 118 kaydı.

## In scope

1. Migration: `inventory.stock_items`'a `reorder_point NUMERIC(14,4) NULL
   CHECK (reorder_point >= 0)` — null = eşik yok, mevcut davranış korunur.
2. `StockItem`'e `ReorderPoint` alanı (negatifse `ArgumentOutOfRangeException`)
   - `Update(...)`'e opsiyonel parametre; repository'nin her SELECT
   listesi, `MapRow`, Add/Update SQL'i güncellendi (bu dosyanın kendi
   yorumu: "any new column requires updating every SELECT list + MapRow +
   Add/Update SQL" — üç yeri de değiştirdim).
3. `IStockMasterService.CreateStockItemAsync`'e opsiyonel `reorderPoint`
   parametresi + yeni `SetReorderPointAsync(stockItemId, reorderPoint)`.
4. `CriticalStockReportItem`'in `CriticalThreshold`'u artık
   `item.ReorderPoint ?? query.CriticalThreshold ?? 0` — geriye dönük
   uyumlu (ReorderPoint null geldiğinde eski davranış birebir korunur).
5. Yönetim ucu: `PUT /api/v1/management/inventory/stock-items/{id}/
   reorder-point`, `inventory.manage`; `CreateStockItemV1`'e de opsiyonel
   `ReorderPoint` alanı eklendi.

## Out of scope

- Düşük stok uyarısı/canlı bildirim ve raporun kendi HTTP ucu —
  V11-RPT-002'nin kapsamı.
- İstemci arayüzü — plan dosyasının kendi "kapsam dışı" kararı.

## Dependencies

- V11-INV-004

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı repository/servis/rapor/HTTP testleri → tüm
  testler yeşil; en az bir testte revert-and-confirm (bu görevde iki gerçek
  hata bulundu ve düzeltildi: rollback/reapply testinin migration 118'i
  yeniden uygulamaması, ve raporun eşik-geçersiz-kılma testinin kendi test
  verisindeki mantık hatası).
- `dotnet test tests/Host/MigrationComposition` → temiz.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID.
