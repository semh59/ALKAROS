# V1-TBL-001 - Implement Table lifecycle and persistence

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement table identity, zone, canonical status transitions and optimistic concurrency.

## Owned surface

- `src/Modules/TableManagement/TableLifecycle/**`, `tests/Modules/TableManagement/TableLifecycle/**`, `database/migrations/V1/V1-TBL-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Table/zone constraints, nullable-zone uniqueness policy, state commands and row-version checks.

## Out of scope

- Table transfer, merge, reservation booking and bill closure coupling.

## Dependencies

- V1-FND-001,V0-DOM-001,V0-DAT-003

## Deliverables

- V1-TBL-001 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Concurrent stale transition is rejected; canonical transitions pass; duplicate table number policy is enforced with NULL semantics.

## Handoff

- V1-TBL-002, V1-TBL-003 and V1-ORD-001.

