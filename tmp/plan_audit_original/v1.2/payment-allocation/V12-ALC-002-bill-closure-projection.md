# V12-ALC-002 - Implement Bill allocation and paid projections

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Compute allocated, paid and change totals and transition Bill status atomically from authoritative payment records.

## Owned surface

- `src/Modules/Billing/PaymentClosure/**`, `tests/Modules/Billing/PaymentClosure/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Projection formulas, rebuild, partially allocated/paid states and closure blockers.

## Out of scope

- Creating payments, fiscal issuance and refund provider calls.

## Dependencies

- V12-ALC-001,V1-BIL-001,V0-DAT-004

## Deliverables

- V12-ALC-002 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Projection rebuild is deterministic; Pending/Unknown/Declined/Cancelled payment cannot close a Bill; exact payable closes once.

## Handoff

- V12-FSC-002 and V12-REC-001.

