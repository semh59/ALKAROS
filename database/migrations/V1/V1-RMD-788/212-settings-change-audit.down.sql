DROP TRIGGER IF EXISTS trg_setting_history_audit ON settings.setting_history;
DROP FUNCTION IF EXISTS settings.audit_setting_change();
