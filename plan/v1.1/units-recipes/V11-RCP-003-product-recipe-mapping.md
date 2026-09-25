# V11-RCP-003 - Katalog ürünü ↔ reçete eşlemesi

- Task ID: V11-RCP-003
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Planned

## Goal

Rakip karşılaştırmasının (Toast/xtraCHEF referansı, 2026-09-16) bulduğu
boşluğun ilk adımı: satış-bazlı "gerçek vs teorik" (AvT) stok varyans
raporu için önce bir katalog ürününün hangi reçeteye karşılık geldiğini
bilmemiz gerekiyor. Bugün `RecipeIngredientItem` yalnızca maliyetlendirme
(`CostSnapshots`) için okunuyor; genel katalog ürünleri ile `Recipe`
arasında hiçbir doğrudan bağlantı yok (yalnız `DailyMenuItem` bir
`RecipeVersion`'a bağlı — günlük özel yemekler, genel katalog değil).
`ProductStockMapping`'in (`Inventory.StockMaster`) kurduğu kalıbın
birebir aynısı, ama hedefi `RecipeId` (aktif versiyon her zaman
`IRecipeVersionRepository.GetActiveVersionAsync` ile çözülür).

## Owned surface

- `src/Modules/Recipes/CatalogMapping/**` (yeni)
- `database/migrations/V11/V11-RCP-003/**` (yeni)
- `src/Host/Experience/Recipes/**` (yeni)
- `tests/Modules/Recipes/CatalogMapping/**` (yeni)
- `tests/Host/Experience/Recipes/**` (yeni)

Sınırlı ek (yollar geri-tik olmadan):

- src/Modules/Recipes/RecipesModule.cs (ilgili modülün sahipliğinde) —
  yeni repository kaydı.
- tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs,
  docs/architecture/module-dependency-rules.md (paylaşılan) — yeni Host
  orkestrasyon kenarı: `ALKAROS.Host.Experience.Recipes` →
  `ALKAROS.Recipes`, `ALKAROS.Identity` (V1-RMD-159/215 emsali, aynı
  diff'te).
- ALKAROS.slnx (paylaşılan) — yeni iki test projesi.

## In scope

1. Yeni migration: `recipe.product_recipe_mappings` (product_id UUID
   PRIMARY KEY, recipe_id UUID NOT NULL REFERENCES recipe.recipes(id),
   is_active BOOLEAN NOT NULL DEFAULT TRUE, notes VARCHAR(255),
   created_at) — `product_stock_mappings`'in aynı stilinde, ama
   product_id TEK BAŞINA birincil anahtar (bir ürünün en fazla bir
   reçetesi olur, `ProductStockMapping`'in aksine — o bir ürünün birden
   fazla stok kalemi tüketebileceği için (product_id, stock_item_id)
   bileşik anahtar kullanıyordu).
2. `ProductRecipeMapping` domain sınıfı (`ProductId`, `RecipeId`,
   `IsActive`, `Notes`, `CreatedAt`) + `IProductRecipeMappingRepository`
   (`AddOrUpdateAsync`, `GetByProductIdAsync`, `GetByProductIdsAsync`,
   `RemoveAsync`) + Postgres implementasyonu — `ProductStockMapping`'in
   kurduğu ham-Npgsql kalıbının birebir aynısı.
3. Yeni Host alanı `src/Host/Experience/Recipes/RecipeCatalogMappingEndpoints.cs`:
   kendi `IEndpointFilter`'ı (`StockMasterEndpointFilter`'ın ~15
   satırlık deseninin birebir kopyası — cross-namespace referans yerine
   kendi kopyası, Host-orkestrasyon kenarını basit tutmak için), aynı
   `alkaros.manager` çerezi + `inventory.manage` izni (yeni bir izin
   icat etmek yerine mevcut olanı kullanıyor — bu eşleme de fiilen bir
   envanter/stok yapılandırma kararı).
   - `POST /api/v1/management/recipes/products/{productId}/mapping`
   - `GET /api/v1/management/recipes/products/{productId}/mapping`
   - `DELETE /api/v1/management/recipes/products/{productId}/mapping`
4. `ApprovedHostOrchestrationEdges`'e yeni girdi:
   `["ALKAROS.Host.Experience.Recipes"] = ["ALKAROS.Identity",
   "ALKAROS.Recipes"]`; `module-dependency-rules.md`'nin Host-orkestrasyon
   tablosuna aynı diff'te satır.

## Out of scope

- Bu eşlemeyi gerçekte KULLANAN sipariş-kabul mantığı (teorik tüketim
  kaydı) — V11-RCP-004'ün kapsamı.
- İstemci arayüzü — plan dosyasının kendi "kapsam dışı" kararı.

## Dependencies

- V11-RCP-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı repository + HTTP testleri → tüm testler
  yeşil; revert-and-confirm ile en az bir test gerçekten kırılıp
  doğrulanır.
- `dotnet test tests/Architecture/ModuleBoundaries` → yeni Host
  orkestrasyon kenarının gerçekten gerekli olduğu revert-and-confirm
  ile kanıtlanır.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID.
