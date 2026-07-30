# V11-MNU-001 - Implement DailyMenu lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement business-date menu creation, item selection, daily price and open/close rules.

## Owned surface

- `src/Modules/Menu/DailyMenuLifecycle/**`, `tests/Modules/Menu/DailyMenuLifecycle/**`, `database/migrations/V11/V11-MNU-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- One menu per service day, recipe reference, daily price and item activation.

## Out of scope

- Inventory counters, production and portion reservation.

## Dependencies

- V1-CAT-001,V1-CAT-002,V11-RCP-001,V0-CMP-002

## Deliverables

- V11-MNU-001 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Service-day uniqueness respects configured timezone/cutoff; closed menu rejects new operational items.

## Handoff

- V11-MNU-002 and V11-PRD-001.

