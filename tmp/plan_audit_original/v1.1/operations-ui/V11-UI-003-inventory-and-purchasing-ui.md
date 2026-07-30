# V11-UI-003 - Implement inventory and purchasing operations UI

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.1 module/schema sections plus named correction dependency.

## Goal

Implement stock balance, purchase receipt, adjustment and waste screens with permissioned reasons.

## Owned surface

- `src/Clients/Cashier/InventoryPurchasing/**`, `tests/Clients/Cashier/InventoryPurchasing/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Balances by location, partial receipt, adjustment, waste, concurrency error and audit reason.

## Out of scope

- Supplier payable/incoming invoice and reporting dashboard.

## Dependencies

- V11-PUR-001,V11-PUR-002,V11-INV-004,V11-INV-005,V11-INV-006

## Deliverables

- V11-UI-003 için production implementation.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- UI never writes balance directly; repeated receipt/adjustment action has one server effect; stale row is rejected.

## Handoff

- V11 exit gate.

