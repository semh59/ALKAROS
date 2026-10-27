# Projection Ownership Contracts

> **Task:** V0-DAT-004
> **Status:** Done
> **Assignee:** codex-v0-dat-004
> **Work type:** decision
> **Source basis:** PDF:II.0-II.1, PDF:III.0-III.2, PDF:II.13-II.15, PDF:III.29-III.40, CORR:C6
> **Date:** 2026-07-30
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

## 1. Projection Registry

| Projection | Source-of-Truth | Writer | Transaction | Drift Detector | Rebuild Path |
| ------------ | ----------------- | -------- | ------------- | ---------------- | -------------- |
| CurrentPrice | Product entity | PriceChanged event handler | Same tx as event | Nightly diff vs products | Full rebuild from products |
| TablePointer | Table entity | The table operation itself (create, status change, transfer, merge, unmerge, reservation) updates `current_order_id`/`current_bill_id` in its own transaction; no `TableStateChanged` event exists | Same tx | The current-pointer endpoint compares the pointers with the authoritative orders and bills on demand; a Host job (`TablePointerRepairHostedService`, every 10 minutes) repairs tables whose order or bill pointer is missing, stale or ghost. A status mismatch alone is never repaired automatically | `PostgresTablePointerProjector.RebuildTablePointersAsync` per table; the pick is the newest open order and bill that is not a check sent to the till, and a table a QR order holds Reserved stays Reserved |
| KitchenStatus | KitchenTicket entity | TicketStateChanged event | Same tx | N/A (real-time) | Full rebuild from tickets |
| MenuCounter | DailyMenu entity | MenuItemSold event | Same tx | Nightly count diff | Full rebuild from orders |
| StockBalance | StockLedgerEntry | StockEntryPosted event | Same tx | Nightly sum vs ledger | Full rebuild from ledger |
| BillTotal | Bill entity | Computed from `bill_items` and bill fields (`Bill.PayableAmount`) and persisted with the bill save; no event is published (no `BillLineChanged`) | Same tx | N/A | Full rebuild from bill_items |
| PaymentTotal | Payment entity | PaymentStateChanged event | Same tx | N/A | Full rebuild from payments |
| AccountBalance | AccountTransaction | TransactionPosted event | Same tx | Nightly sum vs transactions | Full rebuild from transactions |
| SettlementStatus | MealCardSettlement | SettlementStateChanged event | Same tx | N/A | Full rebuild from settlements |

## 2. Rules

1. Every projection MUST have a single authoritative source.
2. Projection updates MUST be atomic with the source event (same transaction).
3. Every projection MUST have a documented rebuild path.
4. Drift detection runs nightly; detected drift triggers automatic rebuild.

> QR `PendingConfirmation` ile ilgili confirmation-status projeksiyonu CORR:C2/C5 çözümüyle tanımlanacaktır
> (V12-QRO-001/002 kapsamı).

## 3. Affected Tasks

- None
