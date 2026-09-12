# V1-WTR-033 - Refactor adım 3/7: CashierHandoffStore çıkarma

- Task ID: V1-WTR-033
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatının devamı ("Başla" → "Durum değerlendirmesi yap ve
devam et", 2026-09-12) —
`docs/engineering/garson-refactor-plan.md`'in Bölüm 1.4'ündeki 7 adımın
**üçüncüsü**: `SendCheckToCashierAsync` + `GetChecksAwaitingPaymentAsync`
(V1-ORD-006, "hesabı kasiyere devret" ailesi), `TableDraftService`
tarafından kullanılmayan, tamamen bağımsız iki metot, kendi
`CashierHandoffStore`'una taşındı. Davranış değişmedi.

## Owned surface

- `src/Host/Experience/Orders/CashierHandoffStore.cs` (yeni).
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-ORD-00x
    ailesinin sahipliğinde) — `SendCheckToCashierAsync`,
    `GetChecksAwaitingPaymentAsync` silindi.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (aynı
    sahiplik) — `POST /{orderId}/send-to-cashier` ve
    `GET /awaiting-payment` artık `OrderManagementStore` yerine
    `CashierHandoffStore`'a bağlı; DI kaydı eklendi.

## Out of scope

- Planın kalan 4 adımı (`TableDraftService` — en büyük parça,
  `OrderSubmissionCoordinator`, `OrderReadStore`, endpoint yeniden
  bağlama) — ayrı görevler, sırayla.

## Dependencies

- V1-WTR-032

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Host/Experience/Orders/TableDraft` → 66/66 (bu iki uç
    noktanın gerçek regresyon testini taşıyan proje —
    `CheckLifecycleHttpTests`).
  - `tests/Host/MigrationComposition` (tam paket, DI kompozisyonu
    dahil) → 135/135.
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
