DROP INDEX IF EXISTS orders.ix_order_items_seat;

ALTER TABLE orders.order_items
    DROP COLUMN IF EXISTS seat_id;
