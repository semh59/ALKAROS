-- Setting history is an audit trail: rows are never deleted or edited, and a setting that still has history cannot be deleted.
-- The only change a row may still undergo is its changed_by being cleared when that user is removed (the existing ON DELETE SET NULL).

ALTER TABLE settings.setting_history
    DROP CONSTRAINT IF EXISTS setting_history_setting_id_fkey;
ALTER TABLE settings.setting_history
    ADD CONSTRAINT fk_setting_history_setting
    FOREIGN KEY (setting_id) REFERENCES settings.settings(setting_id) ON DELETE RESTRICT;

CREATE OR REPLACE FUNCTION settings.enforce_setting_history_immutable()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'settings.setting_history rows cannot be deleted.';
    END IF;
    IF NEW.setting_history_id IS DISTINCT FROM OLD.setting_history_id
       OR NEW.setting_id IS DISTINCT FROM OLD.setting_id
       OR NEW.old_value IS DISTINCT FROM OLD.old_value
       OR NEW.new_value IS DISTINCT FROM OLD.new_value
       OR NEW.reason IS DISTINCT FROM OLD.reason
       OR NEW.changed_at IS DISTINCT FROM OLD.changed_at
       OR NEW.changed_by IS NOT NULL THEN
        RAISE EXCEPTION 'settings.setting_history rows cannot be changed.';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_setting_history_immutable ON settings.setting_history;
CREATE TRIGGER trg_setting_history_immutable
    BEFORE UPDATE OR DELETE ON settings.setting_history
    FOR EACH ROW EXECUTE FUNCTION settings.enforce_setting_history_immutable();

-- NOT VALID: new and changed rows are checked, existing rows are not scanned.
ALTER TABLE settings.business_logo
    ADD CONSTRAINT ck_business_logo_content_type
    CHECK (content_type IN ('image/png', 'image/jpeg', 'image/webp')) NOT VALID;
ALTER TABLE settings.business_logo
    ADD CONSTRAINT ck_business_logo_size
    CHECK (octet_length(content) BETWEEN 1 AND 5242880) NOT VALID;

COMMENT ON COLUMN settings.business_logo.row_version IS
    'Counts replacements of the logo. Not used for concurrency: the HTTP ETag is derived from updated_at.';
