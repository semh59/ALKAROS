-- At most one pending hand-off note per receiving waiter is now a rule of the table, not only of the store.
-- Older duplicates are closed first (the newest one stays pending), then the unique index is created.
UPDATE notifications.serving_handoff_notes n
SET consumed_at = now()
WHERE n.consumed_at IS NULL
  AND EXISTS (
      SELECT 1
      FROM notifications.serving_handoff_notes newer
      WHERE newer.to_user_id = n.to_user_id
        AND newer.consumed_at IS NULL
        AND (newer.created_at, newer.handoff_note_id) > (n.created_at, n.handoff_note_id));

CREATE UNIQUE INDEX IF NOT EXISTS ux_serving_handoff_notes_one_pending
    ON notifications.serving_handoff_notes (to_user_id)
    WHERE consumed_at IS NULL;

-- Two hand-offs to the same waiter at the same moment must not fail on the index: a new pending note closes the
-- earlier one first, and the advisory lock makes the second writer wait for the first to commit.
CREATE OR REPLACE FUNCTION notifications.supersede_pending_handoff_note()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.consumed_at IS NULL THEN
        PERFORM pg_advisory_xact_lock(hashtextextended(NEW.to_user_id::text, 692));
        UPDATE notifications.serving_handoff_notes
        SET consumed_at = now()
        WHERE to_user_id = NEW.to_user_id
          AND consumed_at IS NULL;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_serving_handoff_notes_supersede ON notifications.serving_handoff_notes;
CREATE TRIGGER trg_serving_handoff_notes_supersede
    BEFORE INSERT ON notifications.serving_handoff_notes
    FOR EACH ROW EXECUTE FUNCTION notifications.supersede_pending_handoff_note();

-- The housekeeping sweep deletes by age.
CREATE INDEX IF NOT EXISTS idx_serving_handoff_notes_created_at
    ON notifications.serving_handoff_notes (created_at);
CREATE INDEX IF NOT EXISTS idx_help_requests_created_at
    ON notifications.help_requests (created_at);

-- A help request is the record of who called for what; removing a table or a user must not silently erase it.
ALTER TABLE notifications.help_requests DROP CONSTRAINT IF EXISTS help_requests_table_id_fkey;
ALTER TABLE notifications.help_requests
    ADD CONSTRAINT help_requests_table_id_fkey
    FOREIGN KEY (table_id) REFERENCES table_mgmt.tables(table_id) ON DELETE RESTRICT;

ALTER TABLE notifications.help_requests DROP CONSTRAINT IF EXISTS help_requests_requested_by_user_id_fkey;
ALTER TABLE notifications.help_requests
    ADD CONSTRAINT help_requests_requested_by_user_id_fkey
    FOREIGN KEY (requested_by_user_id) REFERENCES identity.users(user_id) ON DELETE RESTRICT;
