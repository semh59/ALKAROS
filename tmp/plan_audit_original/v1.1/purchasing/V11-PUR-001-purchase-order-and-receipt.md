# V11-PUR-001 - Implement purchase-order items and goods receipt

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement supplier purchase orders with line items and receipt posting into the stock ledger.

## Owned surface

- `src/Modules/Purchasing/OrdersAndReceipts/**`, `tests/Modules/Purchasing/OrdersAndReceipts/**`, `database/migrations/V11/V11-PUR-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Purchase order lines, partial receipt, quantity/unit/price snapshots and PurchaseReceipt movements.

## Out of scope

- Supplier payable account and incoming e-invoice matching.

## Dependencies

- V11-INV-001,V1-CAT-001

## Deliverables

- V11-PUR-001 için production implementation.
- Aynı davranışın otomatik başarı, ret ve concurrency/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Partial and final receipts post exact inventory movements once; over-receipt requires explicit policy result.

## Handoff

- V13-PUR-001.

