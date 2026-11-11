# V1-RMD-975 - Konumlar arası stok transferi

- Task ID: V1-RMD-975
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Yetkili bir yönetici bir stok kaleminin belirli bir miktarını bir konumdan diğerine aktarabilmeli. Transfer, stok defterinde kaynakta çıkış ve hedefte giriş olarak iki gerçek hareket yazar, tek işlemde ve tekrar anahtarıyla; kaynaktaki stoktan fazlası reddedilir, bakiye hiçbir zaman eksiye düşmez.

## Owned surface

- `evidence/V1-RMD-975/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - database/migrations/V1/V1-RMD-975/227-stock-movement-type-transfer.up.sql
  - database/migrations/V1/V1-RMD-975/227-stock-movement-type-transfer.down.sql
  - database/MigrationComposition/order.json
  - src/Host/Composition/Migrations/MigrationManifest.cs
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs
  - src/Modules/Inventory/MovementLedger/StockMovementType.cs
  - src/Modules/Inventory/MovementLedger/StockMovementSourceType.cs
  - src/Modules/Inventory/StockTransfers/IStockTransferService.cs
  - src/Modules/Inventory/StockTransfers/StockTransferModels.cs
  - src/Modules/Inventory/StockTransfers/StockTransferService.cs
  - src/Modules/Inventory/InventoryModule.cs
  - src/Host/Experience/Inventory/StockMasterEndpoints.cs
  - src/Host/Experience/Inventory/StockMasterContracts.cs
  - tests/Host/Experience/Inventory/StockMasterHttpTests.cs
  - tests/Host/Experience/Inventory/StockMasterTestDatabase.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-974

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
