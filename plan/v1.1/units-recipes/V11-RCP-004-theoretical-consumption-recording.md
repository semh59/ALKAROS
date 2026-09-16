# V11-RCP-004 - Sipariş kabulünde teorik tüketim kaydı

- Task ID: V11-RCP-004
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Planned

## Goal

V11-RCP-003'ün kurduğu Product→Recipe eşlemesini fiilen KULLANAN adım:
sipariş kabul edildiğinde (Accept/submit, `OrderStockConsumptionService`),
mevcut `ProductStockMapping`-tabanlı gerçek stok düşümüne EK olarak, eğer
ürünün bir reçetesi varsa, o reçetenin aktif versiyonunun söylediği teorik
tüketimi ayrı, salt-ekleme (append-only) bir gölge kayda yazıyoruz. Bu kayıt
`stock_balances`'a hiç dokunmuyor — yalnızca V11-RPT-003'ün satış-bazlı
gerçek-vs-teorik (AvT) varyans raporu için veri biriktiriyor.

## Owned surface

- `src/Modules/Recipes/TheoreticalConsumption/**` (yeni)
- `database/migrations/V11/V11-RCP-004/**` (yeni)
- `tests/Modules/Recipes/TheoreticalConsumption/**` (yeni)

Sınırlı ek (yollar geri-tik olmadan):

- src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs
  (paylaşılan) — yeni adım: reçete eşlemesi varsa teorik tüketimi aynı
  transaction'da kaydeder.
- tests/Host/Experience/Orders/Confirmation/QrOrderExpiryHostedServiceTests.cs
  (paylaşılan) — `OrderStockConsumptionService`'in yeni constructor
  parametreleri için güncellendi.
- tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs,
  docs/architecture/module-dependency-rules.md (paylaşılan) — mevcut
  `ALKAROS.Host.Experience.Orders.OrderStockConsumption` ve kök
  `ALKAROS.Host.Experience.Orders` Host orkestrasyon kenarlarına
  `ALKAROS.Recipes` eklendi (V1-RMD-159/215 emsali, aynı diff'te).
- src/Modules/Recipes/RecipesModule.cs (ilgili modülün sahipliğinde) — yeni
  repository kaydı.
- database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs (paylaşılan) —
  migration 116 kaydı.

## In scope

1. Yeni migration: `recipe.theoretical_consumption_records` (id,
   order_item_id, product_id, recipe_id, recipe_version_id, stock_item_id,
   quantity, unit_code, recorded_at) — `inventory.stock_movements`'ın aynı
   immutable-ledger stilinde (UPDATE/DELETE'i reddeden trigger), ama
   `stock_balances`'ı hiç etkilemiyor.
2. `TheoreticalConsumptionRecord` domain sınıfı +
   `ITheoreticalConsumptionRecordRepository` (`AppendAsync` — çağıranın
   kendi connection/transaction'ında, `IStockMovementRepository.AppendAsync`
   ile birebir aynı kalıp; `GetTotalsByStockItemAsync` — V11-RPT-003'ün
   kullanacağı toplu okuma) + Postgres implementasyonu.
3. `OrderStockConsumptionService.ConsumeItemsAsync`'e her kalem için yeni
   bir adım: `IProductRecipeMappingRepository`'den eşleme var mı bak (yoksa
   veya pasifse sessizce atla — bu ek/opsiyonel bir izleme, yeni bir kabul
   şartı değil); varsa `IRecipeVersionRepository.GetActiveVersionAsync` ile
   aktif versiyonu al (yoksa sessizce atla); her `RecipeIngredientItem`
   için `teorikMiktar = (item.Quantity / version.YieldQuantity) *
   ingredient.Quantity * (1 + ingredient.LossPercentage/100)`, ilgili
   `StockItem`'i bul (`ingredient.IngredientItemId`), `IUnitConverter.
   TryConvert` ile `StockItem.TrackingUnitCode`'a çevir (çevrilemezse o
   malzeme satırını sessizce atla — bu bilgi amaçlı bir veri, siparişi asla
   engellemez), aynı transaction'da `theoretical_consumption_records`'a
   yaz.
4. `ApprovedHostOrchestrationEdges`'deki mevcut iki girdiye
   (`ALKAROS.Host.Experience.Orders.OrderStockConsumption` ve kök
   `ALKAROS.Host.Experience.Orders`) `ALKAROS.Recipes` eklenir;
   `module-dependency-rules.md`'nin Host-orkestrasyon tablosuna aynı diff'te
   not.

## Out of scope

- AvT raporunun kendisi — V11-RPT-003'ün kapsamı.
- Fiziksel sayım (V11-INV-008) — bu görev yalnızca teorik tarafı besliyor.
- İstemci arayüzü — plan dosyasının kendi "kapsam dışı" kararı.

## Dependencies

- V11-RCP-003

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı repository testleri → tüm testler yeşil; en az
  bir testte revert-and-confirm.
- `dotnet test tests/Architecture/ModuleBoundaries` → genişletilen Host
  orkestrasyon kenarının gerçekten gerekli olduğu revert-and-confirm ile
  kanıtlanır.
- `dotnet test tests/Host/MigrationComposition` → temiz.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID.
