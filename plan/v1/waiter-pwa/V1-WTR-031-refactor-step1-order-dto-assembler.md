# V1-WTR-031 - Refactor adım 1/7: OrderDtoAssembler çıkarma

- Task ID: V1-WTR-031
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in talimatıyla ("Başla", 2026-09-12) —
`docs/engineering/garson-refactor-plan.md`'in Bölüm 1.4'ünde tanımlanan
7 adımlık `OrderManagementStore.cs` (1285 satır) bölünmesinin **birinci
ve en düşük riskli adımı**: `OrderDtoAssembler` çıkarıldı.

Taşınanlar (davranış DEĞİŞMEDİ, yalnız yer değiştirdi):
`WithAvailableStockAsync`, `LoadOrderDtoAsync`, `GetTableNumberAsync`,
`GetActiveOrderByTableIdInternalAsync`, `FindOrderIdBySubmissionAsync`
(iki aşırı yükleme), `MapToDto`, `MapModifiers` — plandaki tam liste.
`OrderManagementStore` artık bunlara constructor'dan enjekte edilen
`OrderDtoAssembler` üzerinden erişiyor (üç servisin ortak bağımlılığı
olacağı plandaki tasarımla birebir).

**Planın öngörmediği, uygulama sırasında bulunan bir ayrıntı:**
`OrderManagementStore.MapModifiers` `internal static` olarak DIŞARIDAN
da çağrılıyordu — `NfcOrderingStore.cs` kendi DTO eşlemesinde bunu
kullanıyordu. Bu çağrı `OrderDtoAssembler.MapModifiers`'a güncellendi;
plan bu harici bağımlılığı satır haritasında görmemişti (yalnız dosya
içi çağıranları saymıştı), ama gerçek etki tek satırlık bir güncelleme
oldu.

## Owned surface

- `src/Host/Experience/Orders/OrderDtoAssembler.cs` (yeni).
- Sınırlı ek:
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-ORD-00x
    ailesinin sahipliğinde) — taşınan 7 metot/aşırı yükleme silindi,
    constructor `OrderDtoAssembler`'a bağımlı hale geldi (3 stok
    repository parametresi kalktı — yalnız `OrderDtoAssembler`
    kullanıyordu), tüm çağrı siteleri güncellendi.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (aynı
    sahiplik) — `OrderDtoAssembler` DI kaydı eklendi
    (`TryAddSingleton`).
  - src/Host/Experience/NfcOrdering/NfcOrderingStore.cs (NFC
    sahipliğinde) — `OrderManagementStore.MapModifiers` çağrısı
    `OrderDtoAssembler.MapModifiers`'a güncellendi (yukarıda
    açıklanan harici bağımlılık).

## Out of scope

- Planın kalan 6 adımı (`ShiftSummaryStore`, `CashierHandoffStore`,
  `TableDraftService`, `OrderSubmissionCoordinator`, `OrderReadStore`,
  endpoint yeniden bağlama) — ayrı görevler, sırayla.
- `waiter-app.js`'in modülerleştirilmesi (planın Bölüm 2'si) — C#
  tarafı bitmeden başlanmayacak (planın kendi Bölüm 3 sıralama kararı).

## Dependencies

- V1-WTR-028

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`:
  - `tests/Host/Experience/Orders/TableDraft` → 66/66.
  - `tests/Host/Experience/Orders/Confirmation` → 19/19.
  - `tests/Host/Experience/Orders/Comp` → 14/14.
  - `tests/Host/Experience/Orders/Void` → 5/5.
  - `tests/Host/Experience/Orders/VoidSent` → 14/14.
  - `tests/Host/Experience/NfcOrdering` → 17/17 (MapModifiers'ın harici
    çağıranı).
  - `tests/Host/MigrationComposition` (tam paket, DI/başlangıç
    kompozisyonu dahil) → 135/135 — yeni `OrderDtoAssembler` kaydının
    gerçek kompozisyonda sorunsuz çözüldüğünün kanıtı.
  - Hepsi regresyon; hiçbiri bu görevde değişmedi, public API/HTTP
    sözleşmesi aynı kaldı.
- `tests/E2E/WaiterPwa` (gerçek Chrome + gerçek Postgres + gerçek Host)
  → 18/18 — gerçek HTTP yüzeyinin de hiç değişmediğinin kanıtı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.

## Handoff

- None
