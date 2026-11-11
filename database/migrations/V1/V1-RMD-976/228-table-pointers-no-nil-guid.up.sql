-- A table pointer is a real id or NULL. The pointer rebuild once wrote the all-zero id for a table with an open order and no
-- bill (or the reverse); clear those and refuse them from now on.
UPDATE table_mgmt.tables SET current_bill_id = NULL, row_version = row_version + 1
    WHERE current_bill_id = '00000000-0000-0000-0000-000000000000';
UPDATE table_mgmt.tables SET current_order_id = NULL, row_version = row_version + 1
    WHERE current_order_id = '00000000-0000-0000-0000-000000000000';
ALTER TABLE table_mgmt.tables DROP CONSTRAINT IF EXISTS ck_tables_pointers_not_nil;
ALTER TABLE table_mgmt.tables ADD CONSTRAINT ck_tables_pointers_not_nil CHECK (
    current_order_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000000'
    AND current_bill_id IS DISTINCT FROM '00000000-0000-0000-0000-000000000000');
