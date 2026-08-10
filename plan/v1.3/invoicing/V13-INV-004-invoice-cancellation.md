# V13-INV-004 - Implement invoice cancellation and correction

- Task ID: V13-INV-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.17
- PDF:II.5.11
- PDF:III.20

## Goal

Issued Invoice'ı silmeden veya Account balance'ı değiştirmeden izinli cancellation/correction intent'ini temsil etmek.

## Owned surface

- `src/Modules/Invoicing/Cancellation/**`, `tests/Modules/Invoicing/Cancellation/**`,
  `database/migrations/V13/V13-INV-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eligibility, immutable source reference, requested action, idempotency ve Pending intent state.

## Out of scope

- Provider transport/status, account effect finalization, timeout reconciliation ve original invoice submission.

## Dependencies

- V13-INV-002
- V13-QNB-002
- V0-DOM-007
- V0-CMP-001

## Deliverables

- `src/Modules/Invoicing/Cancellation/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Issued Invoice değişmez kalır; duplicate request tek Pending intent üretir ve bu görev provider success varsaymaz.
- İptal penceresi geçmişse Pending intent `Expired` terminal durumuna geçer; belge iptal edilemez ve gerekçe kaydı
  tutulur (iade akışı ayrıdır).

## Handoff

- V13-QNB-005
- V13-UI-002
