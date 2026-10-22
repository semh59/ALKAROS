-- V1-RMD-689: a help request used to exist only as a live broadcast; with no manager connected it
-- vanished. The row now carries whether a manager has seen it, so the open ones can be listed again
-- (after a reconnect, on another till) and the waiter can be told when someone took it.
-- The acknowledging user is kept as an audit trail but may be deleted later, hence SET NULL.
ALTER TABLE notifications.help_requests
    ADD COLUMN IF NOT EXISTS acknowledged_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS acknowledged_by_user_id UUID REFERENCES identity.users(user_id) ON DELETE SET NULL;

-- The two queries this serves: the open list (not yet acknowledged, oldest first) and the cooldown
-- check, which now only blocks a repeat while the earlier request is still open.
CREATE INDEX IF NOT EXISTS idx_help_requests_open
    ON notifications.help_requests (created_at)
    WHERE acknowledged_at IS NULL;
