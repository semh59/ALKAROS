# V11-RMD-002 - Module registration and Inventory schema boundary

- Task ID: V11-RMD-002
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

`V11-RMD-001`'in bilerek açık bıraktığı iki maddeyi kapatır (Semih'in
"V1.1 bitti mi bir kontrol et... onu kontrollü düzelterek maine alalım"
isteğinin ve ardından "Düzeltme planı yapalım... bana sormadan bitir"
talimatının devamı):

1. **5 modül mimariden görünmezdi.** `Inventory`, `Recipes`, `Production`,
   `Purchasing`, `Menu` hiçbir `IModule` uygulamıyordu,
   `ModuleRegistry.DefaultCatalog`'da yoktu, `ALKAROS.Host.csproj`'dan hiç
   referans edilmiyordu ve `ModuleBoundaryTests.ModuleAssemblies`'te yoktu
   — yani `DeclaredDependenciesStayWithinTheApprovedEdgeList` ve
   `ActualAssemblyDependenciesAreDeclaredInDependsOn` bu 5 modülü hiç
   denetlemiyordu.
2. **Doğrulanmamış cross-schema yazma iddiası gerçekti.** Bizzat doğruladım:
   `ProductionStockEffectService` `inventory.stock_balances`/
   `inventory.stock_movements`'a 4 yerde, `PurchasingService.ReceiveGoodsAsync`
   ise `inventory.stock_movements`'a 1 yerde ham SQL ile yazıyordu — ikisi de
   Inventory'ye hiçbir proje referansı taşımadan. `tools/consistency-audit`'in
   rule 5'i (cross-schema yazma denetimi) bunu hiç yakalamıyordu çünkü
   `MODULE_SCHEMA` sözlüğü bu 5 modülü hiç içermiyordu — `own_schema is None`
   olduğu için dosyaları tamamen atlıyordu (araç kendisi de eksikti, sadece
   bulgu değil).

`docs/architecture/module-dependency-rules.md` (V0-ARC-001) satır 11 zaten
"Production | Recipe (immutable RecipeVersion), Inventory (portion output)"
direct-call kenarını 2026-08-03'te onaylamıştı ama hiç inşa edilmemişti;
Purchasing tabloda hiç yoktu (2026-09-06 eklendi, satır 27).

## Owned surface

- `plan/v1.1/remediation/V11-RMD-002-module-registration-and-inventory-schema-boundary.md` (yeni)
- `src/BuildingBlocks/Measurements/**` (yeni proje) — `ALKAROS.Measurements.csproj`,
  `UnitDimension.cs`, `UnitDefinition.cs`, `StandardUnits.cs`,
  `IUnitConverter.cs`, `UnitConverter.cs`, `Exceptions.cs`.
- `tests/BuildingBlocks/Measurements/**` (yeni proje) — `ALKAROS.Measurements.Tests.csproj`,
  `DimensionSafeUnitTests.cs` (Recipes'in eski birim testleri klasöründen taşındı).
- `src/Modules/Inventory/InventoryModule.cs` (yeni)
- `src/Modules/Recipes/RecipesModule.cs` (yeni)
- `src/Modules/Production/ProductionModule.cs` (yeni)
- `src/Modules/Purchasing/PurchasingModule.cs` (yeni)
- `src/Modules/Menu/MenuModule.cs` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Modules/Recipes/Units/UnitDimension.cs, UnitDefinition.cs,
    StandardUnits.cs, IUnitConverter.cs, UnitConverter.cs, Exceptions.cs
    (V11-UNT-001 sahipliğinde) — silindi, içerikleri
    src/BuildingBlocks/Measurements/'a taşındı; UnitConversion.cs,
    IUnitConversionRepository.cs, PostgresUnitConversionRepository.cs
    (recipe.unit_conversions'a özgü kalıcılık) yerinde kaldı, artık
    ALKAROS.Measurements'a using ile bağlanıyor.
  - src/Modules/Recipes/ALKAROS.Recipes.csproj, src/Modules/Inventory/ALKAROS.Inventory.csproj
    (V11-UNT-001/V11-INV-004 sahipliğinde) — ikisi de artık
    ALKAROS.Measurements'a referans veriyor; Inventory'nin Recipes'e olan
    doğrudan proje referansı (denetimin tek gerçek ihlal bulduğu şey)
    kaldırıldı.
  - src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs,
    MovementLedger/StockMovementService.cs, StockMaster/StockMasterService.cs,
    WasteRecording/WasteRecordingService.cs (ilgili görevler sahipliğinde) —
    `using ALKAROS.Recipes.Units;` → `using ALKAROS.Measurements;`.
  - src/Modules/Recipes/CostSnapshots/IRecipeCostSnapshotService.cs,
    Versioning/IRecipeLifecycleService.cs, Versioning/RecipeLifecycleService.cs
    (ilgili görevler sahipliğinde) — aynı using değişimi.
  - 18 test dosyası, tests/Modules/Recipes/Versioning ve
    tests/Modules/Inventory altındaki StockMaster, MovementLedger,
    BalanceProjection, MovementReversal, ManualAdjustments, WasteRecording,
    PortionReservations/*, ReservationBalanceProjection, Recipes/CostSnapshots
    (ilgili görevler sahipliğinde) — aynı using değişimi.
  - src/Host/Composition/Modules/ModuleRegistry.cs (V1-FND-001 sahipliğinde)
    — `DefaultCatalog`'a 5 yeni modül eklendi (13 → 18).
  - src/Host/ALKAROS.Host.csproj (V1-FND-001 sahipliğinde) — 5 yeni
    ProjectReference.
  - tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (V0-ARC-001
    sahipliğinde) — `ModuleAssemblies`'e 5 yeni giriş; `ApprovedEdges`'e
    `Production → Inventory`, `Purchasing → Inventory`.
  - tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs
    (V1-FND-001 sahipliğinde) — `DefaultCatalogContainsStandardProductionModules`
    sayısı 13 → 18.
  - docs/architecture/module-dependency-rules.md (V0-ARC-001 sahipliğinde)
    — satır 27 (yeni Purchasing satırı) ve satır 11'in notu (Production
    kenarı artık gerçekten inşa edildi) eklendi.
  - tools/consistency-audit/consistency_audit.py (kurucu sahiplik) —
    `MODULE_SCHEMA`'ya 5 yeni giriş (`Recipes→recipe`, `Inventory→inventory`,
    `Menu→menu`, `Purchasing→purchasing`, `Production→production`) — bu
    olmadan rule 5 bu modülleri hiç denetlemiyordu.
  - src/Modules/Production/StockEffects/ProductionStockEffectService.cs,
    ALKAROS.Production.csproj, ProductionModule.cs (V11-PRD-002 sahipliğinde
    — Module dosyası bu görevin kendisi) — 4 ham SQL yazma yeri
    `IStockBalanceRepository`/`IStockMovementRepository`'nin yeni
    connection/transaction-alan overload'larıyla değiştirildi; Inventory'ye
    proje referansı ve `IModule.DependsOn = ["Inventory"]` eklendi.
  - src/Modules/Purchasing/OrdersAndReceipts/IPurchasingService.cs,
    ALKAROS.Purchasing.csproj, PurchasingModule.cs (V11-PUR-001 sahipliğinde
    — Module dosyası bu görevin kendisi) — aynı desen; ayrıca goods
    receipt artık bir bakiye etkisi de posluyor (önceden yalnızca hareket
    defterine yazıp bakiyeyi hiç güncellemiyordu — aynı kusurun ikinci
    yarısı, düzeltilirken tamamlandı).
  - src/Modules/Inventory/MovementLedger/IStockMovementRepository.cs,
    PostgresStockMovementRepository.cs, BalanceProjection/IStockBalanceRepository.cs,
    PostgresStockBalanceRepository.cs (ilgili görevler sahipliğinde) — her
    ikisine de çağıranın kendi connection/transaction'ını alan bir overload
    eklendi (V0-ARC-001'in `IOrderRepository.ReparentActiveOrdersToTableAsync`
    ile zaten kurduğu desenle aynı) — mevcut tek-parametre overload
    davranışı değişmedi.
  - tests/Modules/Production/StockEffects/ProductionStockEffectsDatabaseTests.cs,
    tests/Modules/Purchasing/OrdersAndReceipts/OrdersAndReceiptsDatabaseTests.cs
    (ilgili görevler sahipliğinde) — gerçek `PostgresStockBalanceRepository`/
    `PostgresStockMovementRepository` enjekte edildi; Purchasing'in fixture'ı
    061-stock-balances migrasyonunu da yüklüyor artık (goods receipt artık
    bakiye de yazdığı için).
  - tests/Modules/Purchasing/OrdersAndReceipts/ALKAROS.Purchasing.OrdersAndReceipts.Tests.csproj
    (ilgili görev sahipliğinde) — V11-INV-002 (061) fixture referansı eklendi.
  - 8 test dosyası, tests/Modules/Inventory altında BalanceProjection,
    MovementLedger, ManualAdjustments, WasteRecording, MovementReversal,
    PortionReservations/CancellationEffects (ilgili görevler sahipliğinde)
    — Fake repository'lere yeni interface overload'ları eklendi (mevcut
    tek-parametre davranışına delege ediyor).
  - 10 dosya, src/Modules/{Recipes,Purchasing,Menu,Production,Inventory}
    altında `PostgresXxxRepository` sınıfları (ilgili görevler sahipliğinde)
    — `tools/consistency-audit` rule 6 (LIMIT eksik) 21 yerde bulundu (bu
    5 modül daha önce hiç taranmadığı için görünmezdi); her `GetAllAsync`/
    `GetByXAsync` listesine `MaxUnpagedRows = 5000` + `LIMIT` +
    aşım kontrolü eklendi (Billing/Kitchen/Tables'ın kendi
    `MaxUnpagedRows`/`LIMIT {n+1}` deseniyle birebir aynı).

## In scope

1. `IUnitConverter`/`UnitConverter`/`UnitDefinition`/`UnitDimension`/
   `StandardUnits`/birim istisnalarını `ALKAROS.Recipes.Units`'ten yeni bir
   `ALKAROS.Measurements` building block'una taşımak — Inventory'nin
   Recipes'e olan TEK gerçek ihtiyacı buydu, artık paylaşılan bir kernel'e
   taşındığı için proje referansı tamamen kalktı (`recipe.unit_conversions`'a
   özgü DB kalıcılığı Recipes'te kaldı, o Recipes'in kendi şemasına özgü).
2. 5 modülün her biri için bir `IModule` uygulaması yazmak (yalnızca
   tamamen çözülebilir bağımlılık grafiği olan servisleri kaydederek —
   `HostComposition.ComposeModules`'ın `ValidateOnBuild: true` ile inşa
   ettiği `ServiceProvider` fail-closed olduğu için); `ModuleRegistry.DefaultCatalog`,
   `ALKAROS.Host.csproj`, `ModuleBoundaryTests.ModuleAssemblies`'e eklemek.
3. `tools/consistency-audit`'in `MODULE_SCHEMA` sözlüğüne 5 yeni girişi
   eklemek — bu olmadan cross-schema yazma denetimi bu modülleri hiç
   görmüyordu.
4. Production'ın ve Purchasing'in Inventory şemasına ham SQL yazmasını
   `IStockBalanceRepository`/`IStockMovementRepository`'nin yeni
   connection/transaction-alan overload'larıyla değiştirmek — V0-ARC-001'in
   zaten onayladığı (Production) ve bu görevle eklenen (Purchasing) direct-call
   kenarını gerçek koda dönüştürmek, batch tamamlama / goods receipt'in kendi
   atomikliğini bozmadan (`conn`/`tx` çağıranın elinde kalıyor).
5. Bu 5 modülü şimdi taramaya başlayan `consistency-audit` rule 6'nın
   bulduğu 21 "LIMIT eksik" bulgusunu düzeltmek — aynı görevin kapsamına
   giren, aynı kök nedenden (modüller hiç denetlenmiyordu) kaynaklanan
   bulgular.

## Out of scope

- **5 modülün HTTP uç nokta katmanı.** Hâlâ hiçbiri Host/Experience üzerinden
  çağrılamıyor — bu `IModule` kaydından tamamen ayrı bir karar
  (`V11-RMD-001`'in de belirttiği gibi, V1'in kendi tarihinde izlenen sıra:
  domain katmanı önce, Host kablolaması ayrı bir dalgada). `V11-PUR-001`'in
  kendi Handoff'u zaten bunu `V14-PUR-001`'e bırakmıştı.
- **Menu'nün Catalog'u okuma şekli** (`PostgresCatalogProductReader`,
  `PostgresCatalogProductPriceReader` — proje referansı olmadan
  `catalog.products`'ı doğrudan SQL ile okuyor). V0-ARC-001 okumaya izin
  veriyor (yalnız yazma yasak); bu bir mimari tercih, düzeltilecek bir
  defekt değil — ayrı bir karar.
- **Production'ın `recipe.recipe_versions`/`recipe.recipe_ingredients`'ı
  ham SQL ile okuması** ve `recipe.unit_conversions`'ı `IUnitConversionRepository`
  yerine ikinci kez ham SQL ile sorgulaması. İkisi de okuma (V0-ARC-001
  izin veriyor), yazma değil — bu görevin kapsamı yalnızca yazma ihlaliydi.
  Kod tekrarı gerçek ama ayrı bir temizlik kararı.
- **`_grRepo.SaveAsync`/`_poRepo.UpdateAsync`'in kendi bağımsız
  connection/transaction'ları.** `ReceiveGoodsAsync`'in "Atomic PostgreSQL
  persistence" yorumu yanıltıcı — bu iki çağrı kendi ayrı bağlantılarını
  açıp kendi transaction'larını hemen commit ediyor, yalnızca stok hareketi
  (şimdi düzeltilen kısım) çağıranın `conn`/`tx`'ini kullanıyordu. Gerçek
  ama önceden var olan, bu görevin dokunmadığı ayrı bir atomiklik açığı —
  düzeltmek `IGoodsReceiptRepository`/`IPurchaseOrderRepository`'ye de aynı
  connection/transaction-alan overload desenini eklemeyi gerektirir; ayrı
  bir karar olarak not edildi, düzeltilmedi.
- `v1.1` → `master` branch birleştirmesi ve `GATE-V11-EXIT`'in kesin
  mühürlenmesi — bu görev yalnızca gate'in son iki açık maddesini kapatıyor;
  birleştirme kararı Semih'i bekliyor.

## Dependencies

- V11-RMD-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml run --build --rm test`:
  **80/80 test projesi (yeni `ALKAROS.Measurements.Tests` dahil), sıfır
  başarısız, 1723 test**. `ALKAROS.Architecture.Tests` (8/8, artık
  Production/Purchasing→Inventory kenarını da doğruluyor),
  `ALKAROS.Host.Tests` (121/121, `DefaultCatalogContainsStandardProductionModules`
  artık 18 modül bekliyor ve `DefaultDiscoveryWithDataSourceBuildsValidProvider`
  gerçek bir `ServiceProvider`'ı `ValidateOnBuild: true` ile 18 modülün
  tamamı için başarıyla inşa ediyor — hiçbir modülün kayıtlı servisi
  çözülemeyen bir bağımlılık taşımıyor).
- `python tools/plan-audit/plan_audit_tool.py validate`: sıfır hata (yeni
  `tests/BuildingBlocks/Measurements/DimensionSafeUnitTests.cs` bu görevin
  owned surface'ında).
- `python tools/plan-audit/plan_audit_tool.py validate-coverage`: sıfır hata.
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi
  bu görevden önce de vardı ve bu görevin dokunmadığı dosyalarda (Turkish
  comment stil ihlalleri, `src/Clients/Cashier/**`); `MODULE_SCHEMA`
  genişlemesinin bulduğu cross-schema yazma (5) ve LIMIT eksikliği (21)
  bulgularının tamamı düzeltildi, sıfıra indi.
- Revert-and-confirm: `MODULE_SCHEMA`'ya 5 modülü eklemeden önce
  `consistency-audit` çalıştırıldığında Production/Purchasing'in
  `inventory.*` yazmaları hiç görünmüyordu (own_schema is None →
  dosyalar tamamen atlanıyordu); eklendikten hemen sonra (kod düzeltmeden
  önce) tam olarak 5 cross-schema yazma bulgusu çıktığı doğrulandı — araç
  düzeltmesi gerçekten etkili.

## Handoff

- V11-GOV-002
