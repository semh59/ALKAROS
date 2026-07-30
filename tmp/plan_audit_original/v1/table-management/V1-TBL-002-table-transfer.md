# V1-TBL-002 - Implement transactional table transfer

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Move an open operational order/bill association between tables while preserving history.

## Owned surface

- `src/Modules/TableManagement/TableTransfer/**`, `tests/Modules/TableManagement/TableTransfer/**`, `database/migrations/V1/V1-TBL-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Source/target validation, optimistic concurrency, history record and audit emission in one transaction.

## Out of scope

- Multi-table merge and payment-in-progress policy not explicitly approved by contract.

## Dependencies

- V1-TBL-001,V1-ORD-001,V1-BIL-001

## Deliverables

- V1-TBL-002 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Successful transfer preserves Order/Bill IDs; occupied target or stale version fails without partial pointer changes.

## Handoff

- V1-TBL-003.

