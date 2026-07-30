# V11-MNU-002 - Implement DailyMenu counter projection

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Project prepared, reserved, consumed, waste and available quantities from authoritative production/inventory records.

## Owned surface

- `src/Modules/Menu/CounterProjection/**`, `tests/Modules/Menu/CounterProjection/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Projection calculation, atomic update hook, rebuild command and drift detection.

## Out of scope

- Authoritative stock movement or reservation mutations.

## Dependencies

- V11-MNU-001,V0-DAT-004,V11-INV-001

## Deliverables

- V11-MNU-002 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Projection can be deleted and rebuilt to the same values; available never becomes an independently writable source.

## Handoff

- V11 exit gate.

