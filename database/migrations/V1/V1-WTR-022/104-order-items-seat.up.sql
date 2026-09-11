-- V1-WTR-022: lets a waiter attribute an order item to a specific seat at
-- the table (seat-based item assignment). Deliberately no FK to
-- table_mgmt.table_seats: that table only exists for a table the floor
-- plan editor has actually laid out with seats, but a table with no seat
-- layout must still accept plain orders (seat_id simply stays null there).
-- Application-level validation (OrderManagementStore) checks a supplied
-- seat_id belongs to the order's own table, the same check
-- PostgresSplitDesignRepository already does for a seat-kind bill
-- allocation owner.
ALTER TABLE orders.order_items
    ADD COLUMN IF NOT EXISTS seat_id UUID NULL;

CREATE INDEX IF NOT EXISTS ix_order_items_seat ON orders.order_items (seat_id)
    WHERE seat_id IS NOT NULL;
