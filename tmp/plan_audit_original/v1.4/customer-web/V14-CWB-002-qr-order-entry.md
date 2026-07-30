# V14-CWB-002 - Build QR order entry

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Let a QR customer compose and submit an order into the pending-confirmation workflow with an explicit final summary.

## Owned surface

- `src/Apps/CustomerWeb/OrderEntry/**`, `tests/Apps/CustomerWeb/OrderEntry/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Cart editing, modifier/note input, price summary, duplicate-submit protection and pending-order status.

## Out of scope

- Direct kitchen submission, customer payment, staff confirmation and menu administration.

## Dependencies

- V14-CWB-001, V14-QRO-001, V14-QRO-002

## Deliverables

- QR customer order-entry interface and API contract tests.
- Duplicate click, expiry, item-unavailable, price-change and table-state tests.

## Acceptance evidence

- Submission creates one pending QR order only; it cannot create a kitchen ticket or reserve stock before the approved confirmation step.

## Handoff

- V14-QRO-003 and V14-OUI-001.
