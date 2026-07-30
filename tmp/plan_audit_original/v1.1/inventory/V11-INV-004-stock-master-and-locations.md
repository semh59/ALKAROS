# V11-INV-004 - Implement StockItem and StockLocation master data

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.1 module/schema sections plus named correction dependency.

## Goal

Implement stock identities, stock types, tracked unit and location configuration.

## Owned surface

- `src/Modules/Inventory/StockMaster/**`, `tests/Modules/Inventory/StockMaster/**`, `database/migrations/V11/V11-INV-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Product-to-stock mapping cardinality, RawMaterial/Portion/Packaging/ServiceItem, base unit and active location.

## Out of scope

- Movements, balances and purchasing UI.

## Dependencies

- V1-CAT-001,V11-UNT-001,V0-DAT-003

## Deliverables

- V11-INV-004 için production implementation.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Every movement target has one valid tracked unit; duplicate/null-location key policy is enforced; inactive item rejects new movement.

## Handoff

- V11-INV-001, V11-INV-002 and V11-PUR-001.

