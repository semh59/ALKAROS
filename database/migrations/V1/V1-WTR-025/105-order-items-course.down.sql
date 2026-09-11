ALTER TABLE orders.order_items
    DROP CONSTRAINT order_items_kitchen_state_check;

ALTER TABLE orders.order_items
    ADD CONSTRAINT order_items_kitchen_state_check
        CHECK (kitchen_state IN ('NotSent', 'Sent', 'Preparing', 'Ready', 'Served', 'Cancelled'));

DROP INDEX IF EXISTS orders.ix_order_items_course;

ALTER TABLE orders.order_items
    DROP COLUMN IF EXISTS course_number;
