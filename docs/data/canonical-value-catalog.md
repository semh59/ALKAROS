# Canonical Value Catalog — approved

> **Task:** V0-DAT-002
> **Status:** Done
> **Assignee:** codex-v0-dat-002
> **Work type:** decision
> **Source basis:** PDF:II.5.1-II.5.15, PDF:III.3-III.40, PDF:II.13-II.15,
> PDF:III.29-III.40, CORR:C2, CORR:C7
> **Access date:** PDF source 2026-07-29; artifact verification 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (PDF baseline + CORR:C2 + CORR:C7 +
> named approver)

This catalog is the single canonical source for every internal text
status/type/method/direction/discriminator column. Database status values
must match the machines below exactly (PDF:II.5). Values absent from this
catalog and absent from the PDF are not internal enums; they are
provider-scoped (section D).

## A. Lifecycle state machines (PDF:I.46A, PDF:II.5.1-II.5.15)

| Entity / column | Canonical values |
| --- | --- |
| Order (`orders.status`, II.5.1) | `Draft, Submitted, PendingConfirmation, Accepted, Rejected, Preparing, Ready, Served, Completed, Cancelled` |
| Bill (`bills.status`, II.5.2) | `Open, PartiallyAllocated, Allocated, PartiallyPaid, Paid, Cancelled, Reopened` |
| Payment (`payments.status`, II.5.3) | `Initiated, Pending, Approved, Declined, Cancelled, Unknown, ReconciliationRequired, Refunded` (+ `PartiallyRefunded`, approved 2026-08-03, V0-DOM-003) |
| FiscalDocument (`fiscal_documents.status`, II.5.4) | `Requested, Pending, Issued, Rejected, Cancelled, Refunded, ReconciliationRequired` |
| ProductionBatch (`production_batches.status`, II.5.5) | `Planned, InProgress, Completed, Cancelled` |
| PortionReservation (`portion_reservations.status`, II.5.6) | `Reserved, Released, Consumed, Waste` |
| KitchenTicket (`kitchen_tickets.status`, II.5.7) | `Queued, Accepted, Preparing, Ready, Cancelled` |
| KitchenTicketItem (`kitchen_ticket_items.status`, II.5.7A) | `Queued, Preparing, Ready, Served, Cancelled` |
| PrintJob (`print_jobs.status`, II.5.8) | `Pending, Printing, Printed, Failed, Retrying, Cancelled` |
| CashSession (`cash_sessions.status`, II.5.9) | `Open, Counting, Closing, Closed, Reconciled` |
| MealCardSettlement (`meal_card_settlements.status`, II.5.10) | `Open, Prepared, Submitted, PartiallySettled, Settled, Disputed, Reconciled` |
| MealCardPayment.settlement_status (II.5.10A) | `Unsettled, IncludedInSettlement, Settled, Disputed` |
| Invoice (`invoices.status`, II.5.11) | `Draft, Validating, PendingProvider, Issued, Rejected, Cancelled, ReconciliationRequired` |
| ReconciliationCase (`reconciliation_cases.status`, II.5.12) | `Open, Investigating, WaitingProvider, Resolved, Dismissed, Escalated` |
| Alert (`alerts.status`, II.5.13) | `Open, Acknowledged, Escalated, Suppressed, Resolved` |
| Table (`tables.current_status`, II.5.15) | `Available, Occupied, Reserved, Cleaning, OutOfService` (application-layer invariant only; source of truth for order/bill ownership is `orders.table_id`/`bills.table_id`) |
| InventoryMovement (II.5.14) | No lifecycle status — immutable ledger entry; corrections are new rows. `MovementType`: `PurchaseReceipt, ProductionOutput, Consumption, Reservation, Release, Waste, Adjustment, Return, Reversal` |

Transition legality is defined in `docs/domain/lifecycle-transition-contracts.md`
(V0-DOM-001). This catalog defines the value sets only.

## B. Polymorphic discriminator columns (CORR:C7)

Every `*_type` / `*_reference_type` discriminator column has a canonical
enumerated list (C7 closed):

| Column | Canonical values |
| --- | --- |
| `inventory.stock_movements.source_type` | `Order, ProductionBatch, GoodsReceipt, ManualAdjustment, StockMovement` (the last for Reversal rows referencing another movement, III.14.4) |
| `inventory.waste_records.source_type` | `PortionReservation, ProductionBatch, StockItem, ManualEntry` |
| `reconciliation.reconciliation_cases.source_reference_type` | `Order, Bill, Payment, FiscalDocument, MealCardPayment, Invoice, PrintJob, OnlineExternalOrder` |
| `observability.alerts.source_reference_type` | `Printer, BackupJob, ReconciliationCase, Payment, FiscalDocument, OnlineProvider, StockBalance` |
| `invoicing.invoice_sources.source_type` | `AccountTransaction` (currently the only real source type; documented as extensible via plan change) |

## C. Other canonical value lists (PDF-defined)

| Field | Canonical values | Source |
| --- | --- | --- |
| `orders.source` | `Cashier, Waiter, Qr, Online` | III.6.1 |
| `order_items.status` | `Draft, Active, Cancelled, Waste, Complimentary` | III.6.2 |
| `order_items.kitchen_state` | `NotSent, Sent, Preparing, Ready, Served, Cancelled` | III.6.2 |
| `order_items.portion_reservation_status` | `NotApplicable, NotReserved, Reserved, Released, Consumed, Waste` | III.6.2 (CORR:C2) |
| `bill_items.line_type` | `Sale, Discount, Complimentary, Refund, Waste, Adjustment` | III.7.2 |
| `payments.payment_method` | `Cash, BankCard, MealCard, CustomerAccount` — `SplitPayment` is not a member (III.8.1, I.26, II.5.3) | III.8.1 |
| `cash_transactions.transaction_type` | `Opening, Sale, CashIn, CashOut, Refund, CountAdjustment, ClosingDifference` | III.9.2 |
| `daily_menus.status` | `Draft, Open, PartiallyConsumed, Closed` | III.11.1 |
| `recipes.status` / `recipe_versions.status` | `Draft, Active, Retired` | III.12.2 |
| `stock_items.stock_type` | `RawMaterial, Portion, Packaging, ServiceItem` | III.14.1 |
| `products.stock_mode` | `Untracked, QuantityTracked, PortionTracked, RecipeDerived` | III.4.3 |
| `products.product_type` | `MenuItem, Modifier, AddOn, Packaging, ServiceItem` | III.4.3 |
| `printers.connection_type` | `USB, Network, LocalAgent` | III.16.2 |
| `alerts.severity` | `Info, Warning, Critical` | III.28.2 |
| `invoices.invoice_type` | `EInvoice, EArchive` | III.20.1 |
| `incoming_invoices.status` | `Received, Validating, Imported, Rejected, Duplicate, ReconciliationRequired` | III.20.1A |
| `account_transactions.transaction_type` | `Charge, Payment, Invoice, Credit, Debit, Adjustment, Refund` | III.18.3 |
| `account_transactions.direction` | Generated column: `Charge→Debit, Invoice→Debit, Payment→Credit, Credit→Credit, Debit→Debit, Refund→Credit, Adjustment→NULL` (sign carried by amount; C3) | III.18.3 |
| `customers` / `customer_accounts` anonymization_status | `Active, AnonymizationPending, Anonymized, RetentionBlocked` | III.18.1/2, III.34 |

## D. NOT canonical (provider-scoped or deferred — no internal enum)

The PDF defines no canonical list for these; no internal enum is created and
no provider value is forced into an internal enum (task out-of-scope rule):

- Provider/device fields: `payment_attempts.status`,
  `fiscal_transactions.status`, `printers.status`, `qr_sessions.status`,
  `qr_orders.status`, `online_external_orders.external_order_status`,
  `online_webhook_events.event_type`, `licenses`/`installations` fields.
- Operational statuses to be owned by their feature tasks
  (V12-PUR-*, V12-QR-*, V14-BKP-*, V14-OBS-*): `purchase_orders.status`,
  `goods_receipts.status`, `backup_jobs.status`, `backup_artifacts.storage_type`,
  `restore_tests.status`, `health_checks.status`.
- Free text / extensible semantics: `alert_type`, `case_type`, `action_type`,
  `event_type`, `adjustment_type`, `price_type`, `selection_type`,
  `customer_type`, `settlement_policy` — each defined by its owning task's
  contract; not canonical here.

## Examples

Positive 1: a `portion_reservation_status` value of `NotReserved` means the
parent order is in `Draft`, `Submitted` or `PendingConfirmation` uniformly
(CORR:C2, approved 2026-08-03); no fourth value was added.

Positive 2: a `stock_movements` row with `movement_type = Reversal` must have
`source_type = StockMovement` and `source_reference_id` pointing at the
compensated movement (III.14.4).

Negative 1: `payment_method = SplitPayment` is rejected at every layer
(III.8.1, I.26, II.5.3); split payment is a composition of `Cash`, `BankCard`,
`MealCard` and/or `CustomerAccount` tenders.

Negative 2: any internal status column written with a value absent from
sections A-C is a schema violation; provider-scoped columns (section D) never
feed an internal lifecycle machine.

## Invariants for consumers

- One canonical definition per internal enum; no duplicate or drifted lists.
- `PartiallyRefunded` (V0-DOM-003) is the only approved addition to the PDF
  canonical sets as of this record.
- Discriminator columns must be constrained to their section-B lists (C7).
- The catalog is not an extension point: additions require a plan change.

## Affected tasks

- Handoff: GATE-V0-EXIT.
- Consumers: V0-DOM-001 (state sets), V0-DOM-003 (`PartiallyRefunded`),
  V1-FND-001 (schema baseline), V12- and V14- feature tasks referencing
  statuses, V1-BIL-001, V1-TBL-001.

## Acceptance evidence

- Every internal text enum field in Part III schemas is defined exactly once
  in sections A-C or explicitly scoped in section D; no ownerless internal
  status field remains.
- Decision record with source, access dates, approver (Semih, 2026-08-03),
  selected result, rejected alternatives and affected task IDs.
