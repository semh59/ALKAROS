ALTER TABLE notifications.help_requests DROP CONSTRAINT IF EXISTS help_requests_requested_by_user_id_fkey;
ALTER TABLE notifications.help_requests
    ADD CONSTRAINT help_requests_requested_by_user_id_fkey
    FOREIGN KEY (requested_by_user_id) REFERENCES identity.users(user_id) ON DELETE CASCADE;

ALTER TABLE notifications.help_requests DROP CONSTRAINT IF EXISTS help_requests_table_id_fkey;
ALTER TABLE notifications.help_requests
    ADD CONSTRAINT help_requests_table_id_fkey
    FOREIGN KEY (table_id) REFERENCES table_mgmt.tables(table_id) ON DELETE CASCADE;

DROP INDEX IF EXISTS notifications.idx_help_requests_created_at;
DROP INDEX IF EXISTS notifications.idx_serving_handoff_notes_created_at;
DROP TRIGGER IF EXISTS trg_serving_handoff_notes_supersede ON notifications.serving_handoff_notes;
DROP FUNCTION IF EXISTS notifications.supersede_pending_handoff_note();
DROP INDEX IF EXISTS notifications.ux_serving_handoff_notes_one_pending;
