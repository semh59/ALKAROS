# Nullable Uniqueness Policy

> **Task:** V0-DAT-003
> **Status:** Done
> **Assignee:** codex-v0-dat-003
> **Work type:** decision
> **Source basis:** PDF:II.0-II.1, PDF:III.0-III.2, PDF:II.13-II.15, PDF:III.29-III.40
> **Date:** 2026-07-30
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

## 1. Decision

PostgreSQL 15+ `NULLS NOT DISTINCT` clause for all nullable unique constraints.

## 2. Rationale

| Approach | Verdict | Reason |
| ---------- | --------- | -------- |
| `NULLS NOT DISTINCT` | ✅ Selected | Native PG 15+ support, clean syntax, treats NULLs as equal for uniqueness |
| Partial index `WHERE col IS NOT NULL` | ❌ Rejected | Allows multiple NULL rows, which violates business intent in most cases |
| NOT NULL + default value | ❌ Rejected | Changes domain semantics; not all nullable columns have a meaningful default |

## 3. Affected Constraints

| Table | Columns | Rule |
| ------- | --------- | ------ |
| `tables` (`table_mgmt`) | `zone_id, table_number` | UNIQUE NULLS NOT DISTINCT (`ux_tables_zone_number`) — zone_id nullable for unzoned tables |
| `stock_balances` | `product_id, location_id` | UNIQUE NULLS NOT DISTINCT — location_id nullable for central stock |
| `printer_routes` | `item_id, product_id, category_id` | UNIQUE NULLS NOT DISTINCT — only one non-null specificity level per row |
| `table_reservations` (`table_mgmt`) | `table_id` where `status = 'Active'` | Partial unique index `ux_table_reservations_active_table` — only one active reservation per table; `expires_at` is not part of the key |
| `table_merges` (`table_mgmt`) | `merged_table_id` where `status = 'Active'` | Partial unique index `ux_table_merges_active_participant` — a table is the active participant of at most one merge |

## 4. Invariants

1. All nullable unique constraints use `NULLS NOT DISTINCT`.
2. No partial index is used for uniqueness enforcement of a nullable column. The two Table Management indexes above are the exception: they key on a status value, not on a nullable column, which `NULLS NOT DISTINCT` cannot express.
3. No NOT NULL is forced on a semantically nullable column.

## 5. Affected Tasks

- None
