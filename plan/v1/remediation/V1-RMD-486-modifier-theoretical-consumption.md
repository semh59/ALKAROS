# V1-RMD-486 - Ekstraların kuramsal tüketime yazılması

- Task ID: V1-RMD-486
- Status: InProgress
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Stok kalemine bağlı bir ekstra (örneğin ekstra peynir) sipariş verilince gerçek stoktan düşüyor (`V1-RMD-152`) ama kuramsal tüketim kaydına yazılmıyor. Gerçek ile kuramsal
tüketimi karşılaştıran rapor (`V11-RPT-003`) bu yüzden o stok kalemi için hiç açıklanamayan, yapay bir fark gösteriyor. Bu görev ekstranın stok bağlantısından türeyen kuramsal
tüketimi, gerçek düşümle aynı işlemde ve aynı kalem kimliğiyle kuramsal kayıt tablosuna yazar; iptal edilen kalemin ekstrası gerçek düşümle birlikte rapordan çıkar.

## Owned surface

- `plan/v1/remediation/V1-RMD-486-modifier-theoretical-consumption.md`
- `evidence/V1-RMD-486/**`
- `database/migrations/V1/V1-RMD-486/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Recipes/TheoreticalConsumption/TheoreticalConsumptionRecord.cs ve src/Modules/Recipes/TheoreticalConsumption/PostgresTheoreticalConsumptionRecordRepository.cs — yalnız ekstra kaynağı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs — yalnız ekstra kuramsal kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Recipes/TheoreticalConsumption/TheoreticalConsumptionRecordDomainTests.cs, tests/Modules/Recipes/TheoreticalConsumption/TheoreticalConsumptionRecordDatabaseTests.cs ve tests/Modules/Recipes/TheoreticalConsumption/ALKAROS.Recipes.TheoreticalConsumption.Tests.csproj — yalnız ekstra kaynağı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs, tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftTestDatabase.cs ve tests/Host/Experience/Orders/TableDraft/ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj — yalnız ekstra kuramsal kaydı testi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Migration: kuramsal kayıt tablosunda reçete ve reçete sürümü boş olabilir, `modifier_id` eklenir; her kayıt ya reçeteden ya ekstradan gelir (CHECK); ekleme-yalnız tetikleyicisi korunur.
- Ekstra bağlantısı olan her ekstra için `adet × çarpan` kadar kayıt, ürünün reçetesi olmasa da yazılır; kalem iptalinde rapor mevcut `order_item_id` kuralıyla ikisini birlikte dışlar.

## Out of scope

- Satılan kalem başına maliyet veya kâr raporu (böyle bir rapor yok); ekstraya maliyet atanması.

## Dependencies

- V1-RMD-152
- V11-RCP-004

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-486/` altındadır.

## Handoff

- None
