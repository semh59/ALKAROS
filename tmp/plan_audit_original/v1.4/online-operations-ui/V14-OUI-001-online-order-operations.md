# V14-OUI-001 - Build online order operations UI

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Give authorized staff one operational queue for QR pending orders and external channel orders without bypassing domain commands.

## Owned surface

- `src/Apps/BackOffice/OnlineOperations/**`, `tests/Apps/BackOffice/OnlineOperations/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Queue filters, source/status visibility, accept/reject/cancel actions, mapping errors and retry status.

## Out of scope

- Domain transition implementation, channel configuration and reconciliation resolution.

## Dependencies

- V14-QRO-003, V14-ONL-003, V14-MAP-002

## Deliverables

- Role-protected operations interface.
- Authorization, concurrency, stale-command and error-presentation tests.

## Acceptance evidence

- Every user action calls the owning module contract and shows its persisted result; stale or unauthorized actions cannot mutate an order.

## Handoff

- V14-REC-001 and V15-REC-001.
