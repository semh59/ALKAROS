# V1-KIT-001 - Implement KitchenTicket and KitchenTicketItem lifecycles

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Create station-scoped kitchen tickets from accepted orders and maintain independent item states.

## Owned surface

- `src/Modules/Kitchen/TicketLifecycle/**`, `tests/Modules/Kitchen/TicketLifecycle/**`, `database/migrations/V1/V1-KIT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Ticket creation, item state commands, parent-ready aggregation and cancellation rules.

## Out of scope

- Printer routing and physical print jobs.

## Dependencies

- V1-ORD-001,V0-DOM-001

## Deliverables

- V1-KIT-001 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Mixed Preparing/Ready item states are valid; parent Ready occurs only when every non-cancelled item is Ready or Served.

## Handoff

- V1-KIT-002 and V1-KIT-003.

