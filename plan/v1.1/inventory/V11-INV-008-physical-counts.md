# V11-INV-008 - Fiziksel sayım (physical/cycle count)

- Task ID: V11-INV-008
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Planned

## Goal

V1.1 tamamlayıcı zincirinin fiziksel sayım adımı: bir yönetici/personel bir
stok kaleminin rafta gerçekte ne kadar olduğunu sayar ve bunu kaydeder. Sayım
sistemdeki mevcut bakiyeden farklıysa, fark `ManualAdjustments`'ın zaten
kurduğu guard'lı transaction deseniyle (`IInventoryTransactionRunner` +
`TryApplyGuardedOnHandDeltaAsync` + `StockMovementSourceType.InventoryAudit`)
gerçek bir düzeltme hareketine dönüşür. Sayım, fark olsun olmasın her zaman
ayrı bir `stock_physical_counts` satırına kaydedilir — eşleşen bir sayım bile
V11-RPT-003'ün gelecekteki AvT (gerçek vs teorik) raporu için "açılış/kapanış
sayımı" kanıtı olarak gerekli.

## Owned surface

- `src/Modules/Inventory/PhysicalCounts/**` (yeni)
- `database/migrations/V11/V11-INV-008/**` (yeni)
- `tests/Modules/Inventory/PhysicalCounts/**` (yeni)

Sınırlı ek (yollar geri-tik olmadan):

- src/Modules/Inventory/InventoryModule.cs (ilgili modülün sahipliğinde) —
  yeni repository/servis kaydı.
- src/Host/Experience/Inventory/StockMasterEndpoints.cs,
  src/Host/Experience/Inventory/StockMasterContracts.cs (paylaşılan) — yeni
  uç: `POST /api/v1/management/inventory/stock-items/{id}/physical-counts`;
  filtreye `RequireActorId` eklendi (Production'ın kendi emsali).
- database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs (paylaşılan) —
  migration 117 kaydı.

## In scope

1. Yeni migration: `inventory.stock_physical_counts` (id, stock_item_id,
   stock_location_id, counted_quantity, previous_on_hand_quantity,
   counted_by_user_id, notes, resulting_movement_id NULL, counted_at) —
   `stock_movements`'ın aynı immutable-ledger stilinde (UPDATE/DELETE
   reddeden trigger).
2. `StockPhysicalCount` domain sınıfı (`Delta` hesaplanan alan) +
   `IPhysicalCountRepository` (`AppendAsync` — çağıranın kendi
   connection/transaction'ında; `GetMostRecentBeforeAsync` — V11-RPT-003'ün
   açılış/kapanış sayımı çözümlemesi için) + Postgres implementasyonu.
3. `IPhysicalCountService`/`PhysicalCountService`: mevcut bakiyeyi oku,
   `delta = counted - currentOnHand`; delta ≠ 0 ise
   `StockMovementType.Adjustment` + `StockMovementSourceType.InventoryAudit`
   ile `ManualAdjustments`'ın aynı guard'lı deseni (delta uygulanamazsa
   `PhysicalCountBalanceGuardFailedException`); delta ne olursa olsun
   `stock_physical_counts` satırını (sonuç hareketine referansla) aynı
   transaction'da yazar.
4. Yönetim ucu: `POST /api/v1/management/inventory/stock-items/{id}/
   physical-counts`, `inventory.manage` (mevcut `StockMasterEndpointFilter`
   altında, ayrı bir Host alanı gerekmiyor — aynı modülün aynı izni).

## Out of scope

- AvT raporunun kendisi — V11-RPT-003'ün kapsamı.
- İstemci arayüzü — plan dosyasının kendi "kapsam dışı" kararı.

## Dependencies

- V11-INV-005

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı repository + servis testleri → tüm testler yeşil;
  en az bir testte revert-and-confirm.
- `dotnet test tests/Host/MigrationComposition` → temiz.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID.
