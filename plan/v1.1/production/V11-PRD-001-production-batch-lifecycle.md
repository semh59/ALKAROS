# V11-PRD-001 - Implement ProductionBatch lifecycle

- Task ID: V11-PRD-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21-I.25
- PDF:II.2.11
- PDF:II.3.8
- PDF:II.5.5
- PDF:III.13

## Goal

Immutable RecipeVersion'a bağlı Planned, InProgress, Completed ve Cancelled ProductionBatch lifecycle'ını uygulamak.

## Owned surface

- `src/Modules/Production/BatchLifecycle/**`, `tests/Modules/Production/BatchLifecycle/**`,
  `database/migrations/V11/V11-PRD-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Toplu geçişler, planned/gerçek miktar ve değişmez tarif bağlantısı.

## Out of scope

- İçerik tüketimi, stok hareketi ve günlük menü sayaçları.

## Dependencies

- V11-RCP-001
- V11-MNU-001
- V0-DOM-001

## Deliverables

- `src/Modules/Production/BatchLifecycle/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tamamlanan/iptal edilen parti, tarifin yeniden atanmasını reddeder; yasak yaşam döngüsü geçişleri yan etkiler olmadan
  başarısız olur.

## Handoff

- V11-PRD-002
