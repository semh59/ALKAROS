# Projection Ownership Contracts

> **Task:** V0-DAT-004
> **Status:** Blocked
> **Assignee:** codex-v0-dat-004
> **Work type:** decision
> **Source basis:** PDF:II.0-II.1, PDF:III.0-III.2, PDF:II.13-II.15, PDF:III.29-III.40, CORR:C6
> **Date:** 2026-07-30

## 1. Projection Registry

| Projection | Source-of-Truth | Writer | Transaction | Drift Detector | Rebuild Path |
| ------------ | ----------------- | -------- | ------------- | ---------------- | -------------- |
| CurrentPrice | Product entity | PriceChanged event handler | Same tx as event | Nightly diff vs products | Full rebuild from products |
| TablePointer | Table entity | TableStateChanged event | Same tx | Real-time check | Full rebuild from tables |
| KitchenStatus | KitchenTicket entity | TicketStateChanged event | Same tx | N/A (real-time) | Full rebuild from tickets |
| MenuCounter | DailyMenu entity | MenuItemSold event | Same tx | Nightly count diff | Full rebuild from orders |
| StockBalance | StockLedgerEntry | StockEntryPosted event | Same tx | Nightly sum vs ledger | Full rebuild from ledger |
| BillTotal | Bill entity | BillLineChanged event | Same tx | N/A | Full rebuild from bill_order_items |
| PaymentTotal | Payment entity | PaymentStateChanged event | Same tx | N/A | Full rebuild from payments |
| AccountBalance | AccountTransaction | TransactionPosted event | Same tx | Nightly sum vs transactions | Full rebuild from transactions |
| SettlementStatus | MealCardSettlement | SettlementStateChanged event | Same tx | N/A | Full rebuild from settlements |

## 2. Rules

1. Every projection MUST have a single authoritative source.
2. Projection updates MUST be atomic with the source event (same transaction).
3. Every projection MUST have a documented rebuild path.
4. Drift detection runs nightly; detected drift triggers automatic rebuild.

> QR `PendingConfirmation` ile ilgili confirmation-status projeksiyonu CORR:C2/C5 çözümüyle tanımlanacaktır
> (V14-QRO-001/002 kapsamı).

## 3. Affected Tasks

- None
