-- V1-WTR-014: a waiter's real-time call for help ("table 5 spilled a
-- tray", "table 8 is complaining", "table 3 needs a manager's approval") —
-- until now the only channel from the floor to management was walking over
-- in person. Delivery itself is SignalR (HelpRequestHub broadcasts to every
-- connected manager/supervisor session), but the row here is what makes a
-- 2-minute per-table cooldown possible across more than one Host instance
-- (an in-memory-only cooldown would reset on every restart and not be
-- shared across replicas) and gives management a real audit trail of who
-- called for what, when — the same reasoning notifications.
-- serving_handoff_notes (V1-WTR-013) already established for this schema.
CREATE TABLE IF NOT EXISTS notifications.help_requests (
    help_request_id UUID NOT NULL,
    table_id UUID NOT NULL REFERENCES table_mgmt.tables(table_id) ON DELETE CASCADE,
    -- Denormalized at write time (not re-derived by a JOIN on every read):
    -- the table's own number can change under a manager's floor-plan edit,
    -- but a request already in flight should keep reading the way it did
    -- the moment the waiter actually pressed the button.
    table_number TEXT NOT NULL,
    requested_by_user_id UUID NOT NULL REFERENCES identity.users(user_id) ON DELETE CASCADE,
    -- Spill, Complaint, Approval, Other - ALKAROS.Host.Experience.Orders.
    -- HelpRequestType is the single source of truth for the allowed set.
    request_type TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_help_requests_request_type
        CHECK (request_type IN ('Spill', 'Complaint', 'Approval', 'Other')),
    PRIMARY KEY (help_request_id)
);

-- The one query this table exists to serve: "was this table's cooldown
-- window already used".
CREATE INDEX IF NOT EXISTS idx_help_requests_table_cooldown
    ON notifications.help_requests (table_id, created_at DESC);
