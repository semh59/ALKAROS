# Reporting Metric Contracts

> **Task:** V0-DOM-008
> **Status:** Done
> **Assignee:** codex-v0-dom-008
> **Work type:** decision
> **Source basis:** PDF:II.2.20, PDF:II.10, PDF:III.31
> **Date:** 2026-07-30

## 1. Metric Registry

| Metric ID | Name | Granularity | Source-of-Truth | Reconciliation Total |
|-----------|------|-------------|------------------|----------------------|
| RPT-001 | Daily Sales | Per store, per day | bills (settled) | SUM(bill_total) = daily_sales |
| RPT-002 | Product Sales | Per product, per day | bill_order_items | SUM(line_total) by product = product_sales |
| RPT-003 | Category Sales | Per category, per day | bill_order_items JOIN products | SUM(line_total) by category = category_sales |
| RPT-004 | Waiter Sales | Per waiter, per day | orders JOIN bills | SUM(bill_total) by waiter = waiter_sales |
| RPT-005 | Table Sales | Per table, per day | orders JOIN bills | SUM(bill_total) by table = table_sales |
| RPT-006 | Sales Rate | Per hour, per day | bills (settled_at) | COUNT(*) by hour = hourly_rate |
| RPT-007 | Portion Count | Per product, per day | kitchen_ticket_items | SUM(quantity) by product = portion_count |
| RPT-008 | Waste Report | Per product, per day | stock_ledger_entries (waste) | SUM(quantity) by product = waste_total |
| RPT-009 | Cash Session | Per session | cash_transactions | SUM(amount) = session_total |
| RPT-010 | Payment Mix | Per method, per day | payments | SUM(amount) by method = method_total |
| RPT-011 | Payment Aging | Per day, per age bucket | payments (captured_at) | SUM(amount) by age = aging_total |
| RPT-012 | Reconciliation | Per case | reconciliation_cases | COUNT(*) = case_count |
| RPT-013 | Printer Usage | Per printer, per day | print_jobs | COUNT(*) by printer = job_count |
| RPT-014 | Backup Status | Per day | backup_logs | COUNT(successful) / COUNT(*) = success_rate |

## 2. Rules
1. All metrics use business date (Europe/Istanbul, 06:00 cutoff per CMP-002).
2. Granularity is the minimum grouping dimension; additional filters allowed but don't change the base granularity.
3. Reconciliation total: each metric MUST have a checksum formula to verify completeness.
4. Undefined metrics remain `Blocked` — no placeholder values.

## 3. Affected Tasks
- V1-RPT-001, V11-RPT-001, V12-RPT-001, V13-RPT-001, V14-RPT-001, V15-RPT-001