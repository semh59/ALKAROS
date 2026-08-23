ALTER TABLE billing.bill_items
    DROP CONSTRAINT IF EXISTS ck_bill_items_nonnegative_amounts;

ALTER TABLE orders.order_items
    DROP CONSTRAINT IF EXISTS ck_order_items_nonnegative_amounts;
