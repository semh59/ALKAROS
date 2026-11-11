ALTER TABLE orders.order_items
    DROP CONSTRAINT ck_order_items_without_kitchen,
    DROP COLUMN without_kitchen_reason_code,
    DROP COLUMN added_without_kitchen_by;
