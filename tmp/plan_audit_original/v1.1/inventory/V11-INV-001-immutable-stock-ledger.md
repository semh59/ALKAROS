# V11-INV-001 - Implement immutable StockMovement ledger

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement typed stock movements with positive magnitude, direction rules and source references.

## Owned surface

- `src/Modules/Inventory/MovementLedger/**`, `tests/Modules/Inventory/MovementLedger/**`, `database/migrations/V11/V11-INV-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Canonical movement types, unit/currency-free quantity rules, source discriminator enforcement and append-only storage.

## Out of scope

- Cached stock balances and reservation transitions.

## Dependencies

- V11-UNT-001,V0-DAT-002,V0-DAT-003

## Deliverables

- V11-INV-001 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Movement rows cannot update/delete; invalid source type or sign is rejected; every movement has deterministic stock effect.

## Handoff

- V11-INV-002, V11-INV-003 and V11-RSV-001.

