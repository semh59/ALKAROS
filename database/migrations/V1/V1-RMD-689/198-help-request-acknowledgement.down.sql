-- Reverse of 198.
DROP INDEX IF EXISTS notifications.idx_help_requests_open;
ALTER TABLE notifications.help_requests
    DROP COLUMN IF EXISTS acknowledged_by_user_id,
    DROP COLUMN IF EXISTS acknowledged_at;
