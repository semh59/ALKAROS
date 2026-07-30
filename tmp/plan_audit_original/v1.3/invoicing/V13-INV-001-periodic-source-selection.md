# V13-INV-001 - Implement periodic invoice source selection

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Select eligible uninvoiced CustomerAccount transactions for one closed billing period without changing balance.

## Owned surface

- `src/Modules/Invoicing/SourceSelection/**`, `tests/Modules/Invoicing/SourceSelection/**`, `database/migrations/V13/V13-INV-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Period boundary, eligibility, locking, source uniqueness and rerun behavior.

## Out of scope

- Invoice rendering/provider submission and incoming invoices.

## Dependencies

- V13-ACC-002,V13-ACC-003,V0-CMP-002

## Deliverables

- V13-INV-001 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- A transaction belongs to at most one non-cancelled invoice source set; rerun returns the same locked set or no work.

## Handoff

- V13-INV-002 and V13-INV-003.

