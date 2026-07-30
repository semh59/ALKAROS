# V11-RSV-003 - Implement cancellation release versus waste decision

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Translate pre-kitchen cancellation to Release and post-preparation cancellation to Waste using explicit kitchen state.

## Owned surface

- `src/Modules/Inventory/PortionReservations/CancellationEffects/**`, `tests/Modules/Inventory/PortionReservations/CancellationEffects/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- State lookup, release/waste movement, audit reason and one-time execution.

## Out of scope

- Payment refund and kitchen item cancellation implementation.

## Dependencies

- V11-RSV-001,V1-KIT-001

## Deliverables

- V11-RSV-003 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Cancelled before preparation restores availability; cancelled after committed preparation does not; retry duplicates neither effect.

## Handoff

- V11 exit gate and V12-ALC-003.

