# V1-BIL-002 - Implement split-bill design persistence

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Persist item, quantity and amount ownership segments without enabling payment execution.

## Owned surface

- `src/Modules/Billing/SplitDesign/**`, `tests/Modules/Billing/SplitDesign/**`, `database/migrations/V1/V1-BIL-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Owner identity, allocated quantity/amount, deterministic rounding residue and double-allocation constraints.

## Out of scope

- PaymentAllocation and mixed tender execution.

## Dependencies

- V1-BIL-001,V0-CMP-002

## Deliverables

- V1-BIL-002 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Item quantity cannot be over-assigned; amount totals match payable amount after deterministic residue assignment.

## Handoff

- V12-ALC-001.

