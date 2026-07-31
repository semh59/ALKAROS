# Bill-Order Cardinality Contract

> **Task:** V0-DOM-002
> **Status:** Done
> **Assignee:** codex-v0-dom-002
> **Work type:** decision
> **Source basis:** PDF:I.11-I.15, PDF:II.2.5, PDF:II.3.3, PDF:II.5.2, PDF:III.7

## 1. Problem Statement

A Bill can reference one or more Orders/OrderItems, and an Order can be split across multiple Bills (e.g., table split, partial payment). The legacy `bills.order_id` single-column foreign key cannot represent N:M or split scenarios without data loss.

## 2. Selected Model: Junction Entity `bill_order_items`

### Schema

```sql
CREATE TABLE bill_order_items (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bill_id         UUID NOT NULL REFERENCES bills(id) ON DELETE RESTRICT,
    order_id        UUID NOT NULL REFERENCES orders(id) ON DELETE RESTRICT,
    order_item_id   UUID REFERENCES order_items(id) ON DELETE RESTRICT,
    amount          NUMERIC(12,2) NOT NULL CHECK (amount > 0),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (bill_id, order_item_id)
);

CREATE INDEX idx_bill_order_items_bill_id ON bill_order_items(bill_id);
CREATE INDEX idx_bill_order_items_order_id ON bill_order_items(order_id);
```

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Junction vs `bills.order_id[]` | Junction table | Relational integrity, FK constraints, queryability |
| `order_item_id` nullable | Nullable | Allows bill split at Order level (not item level) |
| `amount` per row | Per junction row | Supports partial item billing (e.g., half portion on separate bill) |
| `ON DELETE RESTRICT` | Restrict | Prevents orphan bill-order links; explicit cleanup required |
| Unique constraint | `(bill_id, order_item_id)` | Prevents duplicate item billing within same bill |

## 3. Cardinality Rules

### 1:N — One Order, Multiple Bills (Split)
- An Order can have 1..N Bills via `bill_order_items`.
- Each Bill MUST reference at least one OrderItem from the same Order.
- Total billed amount across all Bills for an Order MUST NOT exceed Order total.

### N:1 — Multiple Orders, One Bill (Merge)
- A Bill can reference 1..N Orders via `bill_order_items`.
- Each OrderItem belongs to exactly one Bill at a time.
- An OrderItem cannot be on two Bills simultaneously (enforced by UNIQUE on `order_item_id` across active Bills).

### 1:1 — Simple Case
- Default case: one Order → one Bill.
- Junction table still used for consistency; no special optimization for 1:1.

## 4. Split Scenarios

### Table Split
- Order A (items: 1, 2, 3) → Bill 1 (items 1, 2) + Bill 2 (item 3)
- `bill_order_items`: (bill1, orderA, item1), (bill1, orderA, item2), (bill2, orderA, item3)

### Partial Payment Split
- Order A (total 100 TL) → Bill 1 (50 TL, paid) + Bill 2 (50 TL, pending)
- `bill_order_items`: (bill1, orderA, null, 50), (bill2, orderA, null, 50)
- `order_item_id` is NULL when split is at Order level, not item level.

## 5. Invariants

1. **Total amount invariant**: `SUM(bill_order_items.amount)` for an Order MUST equal the Order total across all Bills.
2. **No double billing**: An `order_item_id` cannot appear in more than one active (non-voided) Bill simultaneously.
3. **At least one item**: Every Bill MUST have at least one `bill_order_items` row.
4. **Consistent Order**: All `bill_order_items` rows for a given Bill MUST reference the same Order (no cross-Order bill merging — this is a business rule, not a schema constraint).
5. **Split limit**: An Order can be split into a maximum of 10 Bills (practical limit to prevent abuse).

## 6. Positive Examples

### Example 1: Simple 1:1
- Order A (items: 1=30, 2=40) → Bill 1 (items 1+2, total 70)
- `bill_order_items`: (bill1, orderA, item1, 30), (bill1, orderA, item2, 40)

### Example 2: Table Split (3-way)
- Order A (items: 1=20, 2=30, 3=50, total 100)
- Bill 1: items 1+2 (50 TL) — Customer 1 pays
- Bill 2: item 3 (50 TL) — Customer 2 pays
- `bill_order_items`: (bill1, orderA, item1, 20), (bill1, orderA, item2, 30), (bill2, orderA, item3, 50)

## 7. Negative Examples

### Example 1: Duplicate item billing
- Bill 1 contains item 1 from Order A
- Bill 2 attempts to contain the same item 1 from Order A
- Result: UNIQUE constraint violation on `(bill_id, order_item_id)` — rejected

### Example 2: Cross-order bill merge
- Bill 1 attempts to reference items from Order A AND Order B
- Result: Business rule violation — rejected at application layer (not schema-enforced)

## 8. Consumer Task Interface

### Input
```json
{
  "billId": "uuid",
  "orderId": "uuid",
  "items": [
    { "orderItemId": "uuid", "amount": 30.00 }
  ]
}
```

### Output
```json
{
  "success": true,
  "billOrderItemIds": ["uuid", "uuid"]
}
```

### Error Output
```json
{
  "success": false,
  "error": "DUPLICATE_ITEM | TOTAL_MISMATCH | SPLIT_LIMIT_EXCEEDED | CROSS_ORDER_MERGE",
  "details": "string"
}
```

### Invariants for Consumers
1. Always use the junction table for Bill-Order relationships. Never write to `bills.order_id`.
2. Check total amount invariant before creating a new Bill for an existing Order.
3. Respect the split limit (max 10 Bills per Order).
4. All Bill-Order operations MUST be within a transaction.