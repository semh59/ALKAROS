# Reporting Metric Contracts — approved decision record

> **Task:** V0-DOM-008
> **Status:** Done
> **Work type:** decision
> **Source basis:** PDF:II.2.20, PDF:II.10, PDF:III.31
> **Access date:** 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

PDF `II.10` (lines 1383-1387) lists the report set: daily sales, product
sales, category sales, waiter performance, table performance, daily menu
sell-through, portion consumption, waste report, cash report, payment mix,
meal card settlement report, customer account aging, invoice aging,
reconciliation backlog, printer failures, backup status — and states
"Reporting is derived data, not source of truth."

## Selected decisions

Every metric contract below binds: granularity, filters, timezone/business
date, source-of-truth and a reconciliation total. Monetary values use
`numeric(18,2)`; the business date and timezone rules of `V0-CMP-002`
apply to every report.

| Metric | Granularity | Source of truth | Formula / reconciliation total |
| --- | --- | --- | --- |
| Daily sales | per business date, per bill closure | `billing.bill_closures` + `bill_items` | `SUM(net/gross per line_type Sale/Refund)`; reconciles to `SUM(bills.payable_amount)` for closed bills |
| Product sales | per product, per business date | `billing.bill_items` | `SUM(quantity)`, `SUM(gross_amount)` per `product_id` |
| Category sales | per category, per business date | `bill_items` via product catalog | `SUM(gross_amount)` per category; category sums equal daily sales total |
| Waiter performance | per user, per business date | `orders`/`bills` waiter attribution | `COUNT(closed bills)`, `SUM(paid_amount)` |
| Table performance | per table, per business date | `billing.bills.table_id` (truth), `table_mgmt.tables` | `COUNT(bills)`, `SUM(payable)`, occupancy minutes per `table_transfers`/order timestamps |
| Daily menu sell-through | per `daily_menu_item`, per business date | `menu.daily_menu_items` + `order_items` | sold `quantity / planned_quantity`; planned from daily menu |
| Portion consumption | per recipe version, per business date | `production_consumptions` (III.13.3) | `SUM(consumed base-unit quantity)` per `recipe_version_id`; reconciles to production batches |
| Waste report | per product, per business date | `inventory_movements` `Waste` type | `SUM(waste quantity)` at moving-average cost (V0-DOM-010) |
| Cash report | per business date, per cash session | cash `payments` + `change_amount` | `SUM(tendered) - SUM(change) = SUM(allocated cash)`; open session flagged, not closed |
| Payment mix | per method, per business date | `payments` + `payment_allocations` | `SUM(allocated_amount)` per method; methods sum to `paid_amount` |
| Meal card settlement | per provider settlement period | `meal_card_settlements` | settlement `SUM(settled_amount)` per provider statement |
| Customer account aging | per customer account, per `snapshot_date` | `customer_account.account_transactions` + snapshots (III.18.4) | age buckets from `invoice_balance`; balance formula from `V0-DOM-007` |
| Invoice aging | per invoice, per business date | `invoices` | outstanding = `invoice total - SUM(payments)`; reconciles to account aging |
| Reconciliation backlog | per day | `reconciliation_cases` | `COUNT(open cases)`; classified by divergence source |
| Printer failures | per printer, per day | `kitchen.print_jobs` status | `COUNT(Failed/Retrying)`; `last_error` recorded |
| Backup status | per backup job | `backup_jobs`/`backup_artifacts` | last success/failure per job; restore test result |

## Cross-cutting rules

1. **Derived data**: every report is derived from ledger rows; a report
   never becomes the source of truth (PDF II.10).
2. **Reconciliation total**: each report carries its reconciliation total;
   mismatch between report and ledger is a bug, not a report feature.
3. **Timezone/business date**: reports aggregate by `Europe/Istanbul`
   service day (V0-CMP-002 cut-off 23:59:59 local).
4. **Immutable inputs**: report inputs are immutable historical rows; a
   corrected source row re-derives the report, it never edits the report.
5. **Undefined term**: a metric without granularity, filter, timezone,
   source-of-truth and reconciliation total stays `Blocked` — it cannot be
   implemented.

## Rejected alternatives

- Reporting tables as source of truth — rejected (PDF II.10).
- Report-local timezone — rejected: single business-timezone rule (V0-CMP-002).
- Metric with ambiguous definition shipped as "best effort" — rejected:
  undefined term remains blocked per acceptance.
- Adding metrics not in PDF II.10 — rejected: scope stays on the listed set.

## Invariants (consumers V1-RPT-001 … V15-RPT-001)

- Every report row is traceable to ledger rows; every report total
  reconciles to its source.
- A report never exposes PII beyond KVKK inventory (V0-CMP-003).
- The same business date + ledger state produce byte-identical report
  numbers on every run.
