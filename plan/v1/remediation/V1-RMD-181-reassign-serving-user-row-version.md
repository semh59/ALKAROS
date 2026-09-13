# V1-RMD-181 - Vardiya devri, row_version'ı atlıyordu

- Task ID: V1-RMD-181
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin backend
boyutundaki bulgusunu kapatır: `PostgresOrderRepository
.ReassignServingUserAsync` (bir garsonun tüm açık masalarını
başka birine devretmesinin, `/transfer-server`'ın SQL katmanı), toplu
bir `UPDATE ... SET serving_user_id = ...` çalıştırıyor ama
`row_version`'a hiç dokunmuyordu.

`UpdateOrderAsync`'in kendi optimistic concurrency kontrolü
(`WHERE order_id = @order_id AND row_version = @expected_row_version`),
`row_version`'ın "bu satırı son okuduğumdan beri hiçbir şey değişmedi"
kanıtı olduğunu varsayıyor. Bir masada hâlâ düzenleme yapan bir garson,
eski `row_version`'ı belleğinde tutarken vardiya devri gerçekleşirse
(sipariş artık başka bir garsona ait), garsonun bir sonraki kaydı hâlâ
o eski `row_version`'la eşleşip GEÇERDİ — devir gerçek bir değişiklik
olmamış gibi davranırdı. Düzeltme: aynı UPDATE'e diğer her sipariş
mutasyonunun kullandığı `row_version = row_version + 1` eklendi.

Kanıt: `tests/Host/Experience/Orders/TableDraft/
OrderManagementTableDraftHttpTests.cs`'e gerçek bir HTTP-seviyesi test
(`TransferringServingUserBumpsTheOrdersRowVersion`) eklendi: bir draft
oluşturup `row_version`'ı okur, `/transfer-server`'ı çağırır, `row_version`'ın
ilerlediğini doğrular. `git stash` ile düzeltme geçici kaldırılıp aynı
test yeniden çalıştırıldı — "stayed at 1" gerçek başarısızlığıyla
doğrulandı; düzeltme geri konunca 1/1 passed.

## Owned surface

- `plan/v1/remediation/V1-RMD-181-reassign-serving-user-row-version.md` (yeni)
- Sınırlı ek:
  - src/Modules/Orders/OrderAggregate/PostgresOrderRepository.cs
    (Orders modülü sahipliğinde) — `ReassignServingUserAsync`'in
    UPDATE'ine `row_version = row_version + 1` eklendi.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
    (Host test sahipliğinde) — yeni
    `TransferringServingUserBumpsTheOrdersRowVersion` testi.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftTestDatabase.cs
    (Host test sahipliğinde) — yeni `GetRowVersionAsync` yardımcısı
    (`GetServingUserIdAsync`'in aynı deseni).

## Out of scope

- Devri `Order.ReassignServer()` + aggregate `SaveAsync(...,
  expectedRowVersion, ...)` üzerinden her siparişi tek tek yükleyip
  kaydederek yapmak: bu bulgu birden fazla siparişi TEK bir atomik SQL
  ifadesiyle değiştirmeyi amaçlıyor (bir vardiya devrinde bir garsonun
  onlarca açık masası olabilir); tek tek aggregate yükleyip kaydetmek
  hem gereksiz karmaşıklık hem de operasyonun atomikliğini bozar.
  `row_version`'ı aynı UPDATE'te artırmak, optimistic concurrency'nin
  gerçek sözleşmesini (bu satır değişti) en az kod ve en az riskle
  karşılıyor.

## Dependencies

- V1-RMD-111

## Acceptance evidence

- `dotnet build tests/Host/Experience/Orders/TableDraft/*.csproj -c Debug`
  → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test
  tests/Host/Experience/Orders/TableDraft/*.csproj`:
  - Yeni test, düzeltme YOKKEN (`git stash` ile geçici kaldırılarak):
    FAILED, gerçek kanıtla ("Expected row_version to advance past 1
    after a serving-user transfer, stayed at 1").
  - Yeni test, düzeltme VARKEN: 1/1 passed.
  - Tüm paket: **67/67 yeşil** (regresyon yok).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None
