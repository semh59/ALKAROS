ALTER TABLE IF EXISTS table_mgmt.tables
    DROP CONSTRAINT IF EXISTS fk_tables_current_order;

DROP TABLE IF EXISTS orders.order_item_modifiers;
DROP TABLE IF EXISTS orders.order_items;
DROP TABLE IF EXISTS orders.order_status_history;
DROP TABLE IF EXISTS orders.orders;
DROP SCHEMA IF EXISTS orders;