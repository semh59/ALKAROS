# V1-TBL-003 - Implement reversible table merge records

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Represent multi-table merge membership and explicit reversal without deleting source tables or orders.

## Owned surface

- `src/Modules/TableManagement/TableMerge/**`, `tests/Modules/TableManagement/TableMerge/**`, `database/migrations/V1/V1-TBL-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Merge group, participants, primary table, history and reversal command.

## Out of scope

- Physical seating reservation and cross-branch table movement.

## Dependencies

- V1-TBL-001,V1-ORD-001,V1-BIL-001,V0-DOM-002

## Deliverables

- V1-TBL-003 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Three-table merge is representable; reversal restores associations only when concurrency preconditions hold; no history is deleted.

## Handoff

- V1 exit gate.

