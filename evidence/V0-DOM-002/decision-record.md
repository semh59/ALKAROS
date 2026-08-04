# V0-DOM-002 Decision Record — approved

- Task: V0-DOM-002
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:I.11-I.15, PDF:II.2.5, PDF:II.3.3, PDF:II.5.2, PDF:III.7
- Access date: PDF source 2026-07-29; artifact verification 2026-08-02
- Result: Approved
- Artifact: `docs/domain/bill-order-cardinality.md`

## Approved decisions

- Junction entity `bill_items` (bill_id, order_item_id); `order_item_id`
  globally unique (no double billing); `(bill_id, order_item_id)` unique.
- `bills.order_id` kept nullable as origin metadata only — single-FK
  dependency removed as invariant.
- Split (N:1) and merge (1:N) represented losslessly via junction rows.

## Evidence

- Decision record written to owned surface artifact; status `Done`.
- `python tools/plan-audit/plan_audit_tool.py validate` re-run after closure.
