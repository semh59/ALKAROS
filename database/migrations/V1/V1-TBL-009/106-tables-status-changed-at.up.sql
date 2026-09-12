-- V1-TBL-009: a passive "recently vacated" indicator for staff — Semih's
-- request (2026-09-12): a practical way to flag a table that might still
-- need wiping down, without adding a manual "mark as cleaning" step to an
-- already busy staff's workload. Existing timestamps couldn't be reused
-- (V1-WTR-019's own "read model, not a new column" trick relies on
-- orders.orders.created_at via current_order_id, but current_order_id is
-- cleared to NULL exactly when a table becomes Available — see
-- PostgresTableRepository.UpdateStatusAsync — so there is nothing left to
-- read once the table is free again). Defaults to NOW() so every existing
-- row gets a real value instead of NULL on migration day.
ALTER TABLE table_mgmt.tables
    ADD COLUMN IF NOT EXISTS status_changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW();
