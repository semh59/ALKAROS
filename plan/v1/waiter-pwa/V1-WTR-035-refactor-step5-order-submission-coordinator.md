# V1-WTR-035 - Refactor adım 5/7: OrderSubmissionCoordinator çıkarma

- Task ID: V1-WTR-035
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatının devamı ("Başla" → "Durum değerlendirmesi yap ve
devam et", 2026-09-12) —
`docs/engineering/garson-refactor-plan.md`'in Bölüm 1.4'ündeki 7 adımın
**beşincisi**: `SubmitOrderAsync` + `FireCourseAsync` ("bir siparişi
mutfağa ileri götür" ailesi) kendi `OrderSubmissionCoordinator`'una
taşındı. Davranış değişmedi.

**Planın öngörmediği bir detay:** `SubmitOrderAsync`, aynı sınıftaki
`GetOrderByIdAsync`'i (yeniden yükleme için) çağırıyordu. `GetOrderByIdAsync`
planda `OrderReadStore`'a (adım 6, henüz çıkarılmadı) ait — bu yüzden
`OrderSubmissionCoordinator`'ın henüz var olmayan bir servise bağımlı
olmasını istemedim. Çözüm: aynı 4 satırlık mantığı kendi private
`ReloadOrderDtoAsync` yardımcısı olarak kopyaladım (dokümante edilmiş,
bilinçli bir küçük tekrar) — çapraz servis bağımlılığı yerine.

## Owned surface

- `src/Host/Experience/Orders/OrderSubmissionCoordinator.cs` (yeni).
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-ORD-00x
    ailesinin sahipliğinde) — `SubmitOrderAsync`, `FireCourseAsync`
    silindi; `_submitHandler`/`_kitchenTickets` bağımlılıkları ve artık
    kullanılmayan 4 using kaldırıldı. Dosya 230 satırdan 123 satıra indi.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (aynı
    sahiplik) — `POST /{orderId}/submit-draft` ve
    `POST /{orderId}/fire-course` artık `OrderManagementStore` yerine
    `OrderSubmissionCoordinator`'a bağlı; DI kaydı eklendi.

## Out of scope

- Planın kalan 2 adımı (`OrderReadStore`, endpoint yeniden bağlamanın
  son temizliği) — ayrı görevler, sırayla.

## Dependencies

- V1-WTR-034

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Host/Experience/Orders/TableDraft` → 66/66 (submit-draft
    akışının kendisi).
  - `tests/Host/Experience/Orders/Confirmation` → 19/19 (fire-course
    dahil).
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
