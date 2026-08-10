# V12-CSH-001 - Implement CashSession lifecycle

- Task ID: V12-CSH-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.2.7
- PDF:II.5.9
- PDF:III.9

## Goal

Terminal/cashier bağlı Open, Counting, Closing, Closed ve Reconciled CashSession geçişlerini uygulamak.

## Owned surface

- `src/Modules/Cash/SessionLifecycle/**`, `tests/Modules/Cash/SessionLifecycle/**`,
  `database/migrations/V12/V12-CSH-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Tek açık oturum politikası, açılış bakiyesi, sayımlar, satır sürümü ve geçiş izinleri.

## Out of scope

- Cash payment defter girişleri ve bildirim uyarıları.

## Dependencies

- V12-PAY-002
- V1-IAM-002
- V0-DOM-001
- V1-CSH-001

## Deliverables

- `src/Modules/Cash/SessionLifecycle/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bir terminal ikinci bir çakışan oturumu açamaz; eski kapatma başarısız olur; Kapalı sessizce yeniden açılamaz.

## Handoff

- V12-CSH-002
