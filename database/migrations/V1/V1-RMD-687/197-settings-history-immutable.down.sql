-- Reverse of 197: history becomes editable again and cascades with its setting, as created in 026.
COMMENT ON COLUMN settings.business_logo.row_version IS NULL;
ALTER TABLE settings.business_logo DROP CONSTRAINT IF EXISTS ck_business_logo_size;
ALTER TABLE settings.business_logo DROP CONSTRAINT IF EXISTS ck_business_logo_content_type;

DROP TRIGGER IF EXISTS trg_setting_history_immutable ON settings.setting_history;
DROP FUNCTION IF EXISTS settings.enforce_setting_history_immutable();

ALTER TABLE settings.setting_history DROP CONSTRAINT IF EXISTS fk_setting_history_setting;
ALTER TABLE settings.setting_history
    ADD CONSTRAINT setting_history_setting_id_fkey
    FOREIGN KEY (setting_id) REFERENCES settings.settings(setting_id) ON DELETE CASCADE;
