# Bill and Order Cardinality — approved decision record

> **Task:** V0-DOM-002
> **Status:** Done
> **Assignee:** codex-v0-dom-002
> **Work type:** decision
> **Source basis:** PDF:I.11-I.15, PDF:II.2.5, PDF:II.3.3, PDF:II.5.2, PDF:III.7
> **Access date:** PDF source 2026-07-29; artifact verification 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (PDF baseline + named approver)

## Selected model

Bill-to-OrderItem junction entity `bill_items`:

- `bill_items.id` surrogate key; `bill_items.bill_id` and
  `bill_items.order_item_id` with a composite unique constraint
  `(bill_id, order_item_id)`.
- Each `order_item` appears in exactly one `bill_items` row: a global unique
  constraint on `bill_items.order_item_id`. An order item is never billed
  twice and never moves bills without an audited reparent action.
- `bills.order_id` is kept as nullable origin metadata (the first order that
  created the Bill) and carries no cardinality constraint. The single-FK
  Bill→Order dependency is removed as an invariant.
- Split: one Order's items are partitioned into two or more Bills by
  `bill_items` rows; each item belongs to exactly one Bill.
- Table merge: two or more Orders are consolidated onto one Bill by inserting
  `bill_items` rows for every participating item; the merged Bill has no
  `order_id` dominance.
- Bill state machine and `Bill.Reopened` (V0-DOM-001) are orthogonal to this
  junction: reopening a Paid Bill to add items inserts new `bill_items` rows
  only through the audited action.

## Why

PDF:I.12 (table/order flow), PDF:I.13.1 (bill creation on table transfer) and
PDF:III.7 (merge) require both directions; a single non-null `bills.order_id`
cannot represent merge or split losslessly. PDF:I.14.3 and PDF:II.3.3 keep
Order/OrderItem as the inventory/kitchen payload carrier, so the junction must
reference items, not orders.

## Examples

Positive 1 (merge): Order A items {a1,a2} and Order B items {b1,b2} merged on
one Bill → `bill_items` rows (bill, a1), (bill, a2), (bill, b1), (bill, b2);
Bill total = sum of all four lines (PDF:III.7).

Positive 2 (split): Order C items {c1,c2,c3} split across Bill X {c1} and
Bill Y {c2,c3} → two junction rows in each; each `order_item` unique across
the table; each Bill computes its own payable from its rows.

Negative 1: `bills.order_id` NOT NULL single FK (N:1 fixed) — cannot represent
the merge example above; rejected.

Negative 2: an `order_item` present in two Bills simultaneously — violates
uniqueness, double-billing risk; rejected.

## Invariants for consumers

- `bill_items` is the only cardinality carrier between Bill and Order.
- `order_item_id` globally unique in `bill_items` (no double billing).
- `(bill_id, order_item_id)` unique.
- Bill payable = sum over its `bill_items` rows at the snapshot rules of the
  pricing decision (V0-DOM-005/V1-BIL-*).
- Table transfer and merge create/delete junction rows transactionally with
  Bill state changes (PDF:I.13.1, PDF:III.7).

## Affected tasks

- Handoff: V1-BIL-001, V1-BIL-002.
- Consumers: V1-TBL-001 (table merge), V0-DOM-003 (refund ledger), V0-DAT-002
  (catalog), V12-PAY-001 (payment allocation on Bill payable).

## Acceptance evidence

- 1:N (merge), N:1 (split) and split scenarios represented losslessly with
  junction rows; single `bills.order_id` dependency removed.
- Decision record with source, access dates, approver (Semih, 2026-08-03),
  selected result, rejected alternatives and affected task IDs.
