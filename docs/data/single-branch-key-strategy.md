# Single-Branch and Business Key Strategy

> **Task:** V0-DAT-005
> **Status:** Done
> **Assignee:** codex-v0-dat-005
> **Work type:** decision
> **Source basis:** PDF:I.1.5, PDF:II.0, PDF:III.2
> **Date:** 2026-07-30
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

## 1. Key Strategy

| Entity | Primary Key | Business Key | Format |
| ------- | ------------- | ------------- | -------- |
| stores | UUID | store_code | `STR-{n}` |
| users | UUID | username | alphanumeric |
| orders | UUID | order_number | `ORD-{yyMMddHHmm}-{seq}` |
| bills | UUID | bill_number | `BIL-{yyMMdd}-{seq}` |
| payments | UUID | payment_ref | provider reference |
| fiscal_documents | UUID | document_number | fiscal device assigned |
| invoices | UUID | invoice_number | sequence per customer |
| products | UUID | sku | alphanumeric |
| tables | UUID | table_number | numeric per store |
| cash_sessions | UUID | session_number | `CSH-{yyMMdd}-{seq}` |

## 2. Rules

1. UUID v7 for all primary keys (time-ordered, globally unique).
2. Business keys are unique per store where applicable.
3. Merging multi-branch data: UUID guarantees no collision; business keys prefixed with `store_code`.
4. Single-branch default: all data isolated by `store_id` column (tenant filter).

## 3. Affected Tasks

- V1-FND-001, V20-MIG-001
