# Nullable Uniqueness Policy

> **Task:** V0-DAT-003
> **Status:** InProgress
> **Assignee:** codex-v0-dat-003
> **Work type:** decision
> **Source basis:** PDF:II.0-II.1, PDF:III.0-III.2, PDF:II.13-II.15, PDF:III.29-III.40
> **Date:** 2026-07-30

## 1. Decision

PostgreSQL 15+ `NULLS NOT DISTINCT` clause for all nullable unique constraints.

## 2. Rationale

| Approach | Verdict | Reason |
|----------|---------|--------|
| `NULLS NOT DISTINCT` | ✅ Selected | Native PG 15+ support, clean syntax, treats NULLs as equal for uniqueness |
| Partial index `WHERE col IS NOT NULL` | ❌ Rejected | Allows multiple NULL rows, which violates business intent in most cases |
| NOT NULL + default value | ❌ Rejected | Changes domain semantics; not all nullable columns have a meaningful default |

## 3. Affected Constraints

| Table | Columns | Rule |
|-------|---------|------|
| `zone_tables` | `zone_id, table_number` | UNIQUE NULLS NOT DISTINCT — zone_id nullable for unzoned tables |
| `stock_balances` | `product_id, location_id` | UNIQUE NULLS NOT DISTINCT — location_id nullable for central stock |
| `printer_routes` | `item_id, product_id, category_id` | UNIQUE NULLS NOT DISTINCT — only one non-null specificity level per row |
| `reservations` | `table_id, expires_at` | UNIQUE NULLS NOT DISTINCT — only one active reservation per table |

## 4. Invariants

1. All nullable unique constraints use `NULLS NOT DISTINCT`.
2. No partial index is used for uniqueness enforcement.
3. No NOT NULL is forced on a semantically nullable column.

## 5. Affected Tasks

- None