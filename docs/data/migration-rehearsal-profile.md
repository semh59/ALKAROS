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
- Migration 201 deletes screensaver, pairing and session rows that break the new checks, adds those checks, changes one column type on the single-row screensaver table and builds two indexes (a partial one on terminals, one on the account transaction ledger); only the ledger index reads a large table, so build it on the Large and Stress classes and note the time; control check: both indexes and the three constraints exist.
- Migration 200 closes older duplicate pending hand-off notes, adds a unique partial index, a trigger and two age indexes on small notification tables, and replaces two foreign keys on the help request table; no rewrite, no rehearsal needed beyond the control check that the index and trigger exist.
- Migration 199 adds one nullable column and a check to the single-row push signing key table and relaxes one NOT NULL; no rewrite, no rehearsal needed beyond the control check that the envelope column exists.
- Migration 198 adds two nullable columns and a partial index to the small help request table; no rewrite, no rehearsal needed beyond the control check that both columns exist.
- Migration 197 replaces the setting history foreign key (one scan of the history table, which stays small) and adds two NOT VALID checks on the single-row logo table, so no rewrite; its control check is that the history trigger exists and a delete of a setting with history is refused.
- Migration 195 adds one CHECK (not validated, so no table scan) and replaces two trigger functions on the authorization grant and delegation tables and creates the delegation transition trigger; no row is rewritten and it needs no rehearsal beyond the control check that both triggers exist afterwards.
- Migration 194 adds a statement trigger to the parent and to every existing partition (metadata only, no row is touched); its control check is that the trigger count equals the partition count plus one.
