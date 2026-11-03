-- V1-RMD-788 (PO 2026-11-03): every change of a setting - the cash-out approval threshold and the self-approval
-- switch included - is written to the audit log. The audit row is written by the same statement that appends the
-- setting_history row, so a setting can never change without its audit event (and the reverse).
-- Registration rows (old_value IS NULL) are not changes and are not audited.
CREATE OR REPLACE FUNCTION settings.audit_setting_change() RETURNS trigger AS $$
BEGIN
    INSERT INTO audit.audit_events (
        id, event_name, aggregate_type, aggregate_id, actor_id, actor_type, reason, correlation_id,
        before_state_json, after_state_json, occurred_at)
    SELECT gen_random_uuid(),
           CASE WHEN NEW.new_value = '[Deactivated]' THEN 'settings.deactivated' ELSE 'settings.value_changed' END,
           'Setting', NEW.setting_id, NEW.changed_by,
           CASE WHEN NEW.changed_by IS NULL THEN 'System' ELSE 'User' END,
           NEW.reason, replace(NEW.setting_id::text, '-', ''),
           jsonb_build_object('key', s.setting_key, 'value', NEW.old_value),
           jsonb_build_object('key', s.setting_key, 'value', NEW.new_value),
           NEW.changed_at
    FROM settings.settings s
    WHERE s.setting_id = NEW.setting_id;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_setting_history_audit ON settings.setting_history;
CREATE TRIGGER trg_setting_history_audit
    AFTER INSERT ON settings.setting_history
    FOR EACH ROW WHEN (NEW.old_value IS NOT NULL)
    EXECUTE FUNCTION settings.audit_setting_change();
