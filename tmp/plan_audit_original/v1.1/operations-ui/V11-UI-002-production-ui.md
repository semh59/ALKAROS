# V11-UI-002 - Implement production batch UI

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.1 module/schema sections plus named correction dependency.

## Goal

Implement planned/start/complete/cancel production workflow with recipe and stock-effect preview.

## Owned surface

- `src/Clients/Cashier/Production/**`, `tests/Clients/Cashier/Production/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Batch state, planned/actual quantity, immutable recipe display, insufficient-stock and duplicate-complete handling.

## Out of scope

- Recipe editing and inventory adjustment.

## Dependencies

- V11-PRD-001,V11-PRD-002,V11-UI-001

## Deliverables

- V11-UI-002 için production implementation.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Double complete creates one output; referenced recipe is read-only; failure leaves batch recoverable and explains missing stock.

## Handoff

- V11-UI-003.

