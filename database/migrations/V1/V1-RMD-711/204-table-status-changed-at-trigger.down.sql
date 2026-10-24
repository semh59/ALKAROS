DROP TRIGGER IF EXISTS trg_tables_status_changed_at ON table_mgmt.tables;
DROP FUNCTION IF EXISTS table_mgmt.touch_status_changed_at();
