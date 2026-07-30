# V1-BIL-001 - Implement Bill foundation and source links

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement Bill, BillItem and the cardinality-safe Order/OrderItem source relationship selected by V0-DOM-002.

## Owned surface

- `src/Modules/Billing/BillFoundation/**`, `tests/Modules/Billing/BillFoundation/**`, `database/migrations/V1/V1-BIL-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Open bill creation, source links, monetary snapshots and non-payment status subset.

## Out of scope

- Payment allocation, paid closure and refund.

## Dependencies

- V1-ORD-001,V0-DOM-002,V0-CMP-002

## Deliverables

- V1-BIL-001 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- One Bill can source multiple Orders and one Order can split across Bills without duplicated quantity or amount.

## Handoff

- V1-BIL-002, V1-TBL-002 and V12-ALC-002.

