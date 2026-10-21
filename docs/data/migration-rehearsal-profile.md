# Migration Rehearsal Profile

> **Task:** V0-DAT-006
> **Status:** Done
> **Assignee:** codex-v0-dat-006
> **Work type:** decision
> **Source basis:** PDF:I.45.1, PDF:III.39-III.40, EXT:POSTGRESQL-18.4, CORR:C17
> **Date:** 2026-07-30
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

## 1. Dataset Classes

| Class | Description | Privacy | Row Volume |
| ------- | ------------- | --------- | ------------ |
| Small | Single store, 1 day | Sanitized | 100-1,000 |
| Medium | Single store, 1 month | Sanitized | 1,000-50,000 |
| Large | Multi-store, 1 year | Sanitized | 50,000-500,000 |
| Stress | Multi-store, 5 years | Synthetic | 500,000-5,000,000 |
| Invalid | Deliberately broken fixtures | N/A | 10-100 |

## 2. Control Total Queries

- Financial: SUM(bill_total) by store, by day
- Stock: SUM(quantity) by product, by location
- Count: COUNT(*) by entity type
- Integrity: FK validation, orphan check, cycle check

## 3. Rules

1. All rehearsal data MUST be sanitized (no PII).
2. Invalid fixtures MUST produce expected rejections.
3. Control totals MUST be documented before and after migration.

## 4. Affected Tasks

- V20-MIG-001, V20-MIG-002

## 5. Addendum — migrations that rewrite or guard a large table (V1-RMD-645)

- Migration 173 copies the whole `audit.audit_events` table into a yearly-partitioned table inside one transaction and keeps the table locked for the duration of the copy. Rehearse it on the Large and Stress classes with the audit log filled to that volume and note the lock time before running it on a store that has been live for years; control total: `count(*)` before and after (the migration itself aborts when they differ).
- Migration 195 adds one CHECK (not validated, so no table scan) and replaces two trigger functions on the authorization grant and delegation tables; no row is rewritten and it needs no rehearsal beyond the control check that both triggers exist afterwards.
- Migration 194 adds a statement trigger to the parent and to every existing partition (metadata only, no row is touched); its control check is that the trigger count equals the partition count plus one.
