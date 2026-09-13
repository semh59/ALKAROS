# V1-WTR-034 - Refactor adım 4/7: TableDraftService çıkarma (en büyük parça)

- Task ID: V1-WTR-034
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatının devamı ("Başla" → "Durum değerlendirmesi yap ve
devam et", 2026-09-12) —
`docs/engineering/garson-refactor-plan.md`'in Bölüm 1.4'ündeki 7 adımın
**dördüncüsü ve en büyük/en riskli parçası**: `CreateOrUpdateTableDraftAsync`

- kendi 8 private yardımcı metodu (`ResolveModifiersAsync`,
`ResolveValidSeatIdsAsync`, `ResolveApplicableModifierGroupsAsync`,
`ValidateModifierGroupSelections`, `ValidateRequestBounds`,
`ItemContentUnchanged`, `BuildModifiers`, `ResolveCatalogProductsAsync`)
- ilgili 4 sabit + `TableCheckAlreadyOpenException`, planın öngördüğü
gibi kendi klasörüne (`TableDraft/`) taşındı. Davranış değişmedi —
yalnız kod taşındı.

Diğer 3 adımdan (V1-WTR-031/032/033) sonra, plandaki sıralamaya uygun
şekilde yapıldı — bu üçü sorunsuz geçtikten sonra desen doğrulanmış
oluyordu.

## Owned surface

- `src/Host/Experience/Orders/TableDraft/TableDraftService.cs` (yeni).
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-ORD-00x
    ailesinin sahipliğinde) — `CreateOrUpdateTableDraftAsync`, 8 private
    yardımcı, 4 sabit ve `TableCheckAlreadyOpenException` silindi.
    Dosya 878 satırdan 230 satıra indi (orijinal 1285'in %18'i).
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (aynı
    sahiplik) — `POST /table-draft` artık `OrderManagementStore` yerine
    `TableDraftService`'e bağlı; DI kaydı eklendi; yeni
    `ALKAROS.Host.Experience.Orders.TableDraft` using'i eklendi
    (istisna eşleme switch'i `TableCheckAlreadyOpenException`'ı hâlâ
    bare adla referans veriyor).

## Out of scope

- Planın kalan 3 adımı (`OrderSubmissionCoordinator`, `OrderReadStore`,
  endpoint yeniden bağlamanın son temizliği) — ayrı görevler, sırayla.

## Dependencies

- V1-WTR-033

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata (ilk denemede).
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Host/Experience/Orders/TableDraft` → 66/66 (bu metodun asıl,
    en kapsamlı regresyon testi).
  - `tests/Host/Experience/Orders/{Confirmation,Comp,Void,VoidSent}` →
    19/14/5/14, hepsi yeşil.
  - `tests/Host/Experience/NfcOrdering` → 17/17.
  - `tests/Host/Experience/Billing` → 18/18.
  - `tests/Host/MigrationComposition` (tam paket, DI kompozisyonu
    dahil) → 135/135.
- `tests/E2E/WaiterPwa` (gerçek Chrome + gerçek Postgres + gerçek Host)
  → **3 kez art arda çalıştırıldı, 18/18 her seferinde** — bu adımın
  taşıdığı kod (masa siparişi oluşturma/birleştirme, tam sipariş akışının
  kalbi) en riskli olduğu için tek koşumla yetinilmedi.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
