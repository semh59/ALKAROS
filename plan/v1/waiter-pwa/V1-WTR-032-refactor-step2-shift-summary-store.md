# V1-WTR-032 - Refactor adım 2/7: ShiftSummaryStore çıkarma

- Task ID: V1-WTR-032
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatının devamı ("Başla", 2026-09-12) —
`docs/engineering/garson-refactor-plan.md`'in Bölüm 1.4'ündeki 7 adımın
**ikincisi**: `GetMyShiftSummaryAsync` (V1-WTR-021), dosyanın en bağımsız
metodu, kendi `ShiftSummaryStore`'una taşındı. Davranış değişmedi.

## Owned surface

- `src/Host/Experience/Orders/ShiftSummaryStore.cs` (yeni).
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-ORD-00x
    ailesinin sahipliğinde) — `GetMyShiftSummaryAsync` silindi.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (aynı
    sahiplik) — `GET /my-shift-summary` artık `OrderManagementStore`
    yerine `ShiftSummaryStore`'a bağlı; DI kaydı eklendi.

## Out of scope

- Planın kalan 5 adımı (`CashierHandoffStore`, `TableDraftService`,
  `OrderSubmissionCoordinator`, `OrderReadStore`, endpoint yeniden
  bağlama) — ayrı görevler, sırayla.

## Dependencies

- V1-WTR-031

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Host/Experience/Billing` → 18/18 (`/my-shift-summary` uç
    noktasının gerçek regresyon testini taşıyan proje —
    `AVoluntaryTipTodayAppearsInTheServingWaitersShiftSummary` ve
    komşuları).
  - `tests/Host/MigrationComposition` (tam paket, DI kompozisyonu
    dahil) → 135/135 — yeni `ShiftSummaryStore` kaydının gerçek
    kompozisyonda sorunsuz çözüldüğünün kanıtı.
- `tests/E2E/WaiterPwa` (gerçek Chrome + gerçek Postgres + gerçek Host)
  → 18/18.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
