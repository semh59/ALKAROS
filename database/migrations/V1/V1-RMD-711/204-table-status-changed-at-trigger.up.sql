CREATE OR REPLACE FUNCTION table_mgmt.touch_status_changed_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.current_status IS DISTINCT FROM OLD.current_status THEN
        NEW.status_changed_at := now();
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_tables_status_changed_at ON table_mgmt.tables;
CREATE TRIGGER trg_tables_status_changed_at
    BEFORE UPDATE OF current_status ON table_mgmt.tables
    FOR EACH ROW
    EXECUTE FUNCTION table_mgmt.touch_status_changed_at();
