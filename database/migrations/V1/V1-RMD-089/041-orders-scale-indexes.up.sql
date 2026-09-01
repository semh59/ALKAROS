-- V1-RMD-089: orders scale indexes.
-- Measured at 1,000,000 orders / 3,000,000 items on tuned PostgreSQL 18:
--   business-day revenue aggregate : 134 ms parallel seq scan  -> 13.6 ms bitmap heap scan
--   most-recent open order for table: 158 ms parallel seq scan  -> 0.08 ms index scan
-- Plain CREATE INDEX (not CONCURRENTLY) because migrations run inside the
-- composition transaction. On a database that is already large, build the
-- equivalent indexes CONCURRENTLY by hand during a maintenance window instead.

-- Date-range reporting / analytics scans over orders.orders
-- (revenue per business day, last-N-days completed, service-window filters).
CREATE INDEX IF NOT EXISTS ix_orders_created_at
    ON orders.orders (created_at);

-- "Most recent open order for a table". ix_orders_table (table_id) alone stops
-- being selective once Completed / Cancelled history accumulates on a table.
-- This partial index only contains orders still in a non-terminal state.
CREATE INDEX IF NOT EXISTS ix_orders_table_open
    ON orders.orders (table_id, created_at DESC)
    WHERE status IN ('Draft', 'Submitted', 'PendingConfirmation', 'Accepted', 'Preparing', 'Ready', 'Served');
