-- A line the cashier added after the fact (the guest ate it, the waiter never entered it): no kitchen ticket, stock still
-- consumed. The adding user and a reason code are kept on the line itself so orders, bills and reports can tell it apart.
ALTER TABLE orders.order_items
    ADD COLUMN added_without_kitchen_by uuid,
    ADD COLUMN without_kitchen_reason_code text,
    ADD CONSTRAINT ck_order_items_without_kitchen CHECK (
        (added_without_kitchen_by IS NULL AND without_kitchen_reason_code IS NULL)
        OR (added_without_kitchen_by IS NOT NULL
            AND without_kitchen_reason_code IN ('waiter-forgot', 'customer-asked', 'other')));
