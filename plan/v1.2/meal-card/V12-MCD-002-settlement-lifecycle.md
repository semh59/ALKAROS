# V12-MCD-002 - Implement MealCardSettlement lifecycle

- Task ID: V12-MCD-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.14
- PDF:II.3.10
- PDF:II.5.10
- PDF:III.17
- CORR:C6

## Goal

Meal-card payment'larını provider settlement dönemlerinde gruplamak, parent/child durumunu atomik güncellemek ve
mismatch evidence event'i üretmek.

## Owned surface

- `src/Modules/MealCard/Settlements/**`, `tests/Modules/MealCard/Settlements/**`,
  `database/migrations/V12/V12-MCD-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Period uniqueness, item membership, parent totals, child projection, disputed result ve typed mismatch evidence event.

## Out of scope

- CustomerAccount, BankCard reconciliation ve ReconciliationCase oluşturma.

## Dependencies

- V12-MCD-001
- V0-MCD-001
- V0-DAT-004
- V0-DOM-001
- V1-SEC-002

## Deliverables

- `src/Modules/MealCard/Settlements/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Parent Settled ile child durumları drift edemez; rebuild saklanan toplamı üretir; mismatch aynı evidence event'i
  idempotent olarak yayınlar.
- `V0-MCD-001` onaylı provider listesini boş kapatır ve `V12-MCD-001` aynı evidence ile `NotApplicable` olursa bu task
  da `NotApplicable` olur; settlement schema veya dead code oluşturulmaz.

## Handoff

- V12-REC-001
