-- V1-RMD-724: the 077 and 105 down scripts re-add the old CHECK value sets and
-- crash when an 'Nfc' order or a 'Held' item exists. This migration owns the
-- two constraints from here on so its down script can map those rows to the
-- closest older value first; re-asserting the current sets here changes nothing.
ALTER TABLE orders.orders DROP CONSTRAINT IF EXISTS orders_source_check;
ALTER TABLE orders.orders ADD CONSTRAINT orders_source_check
    CHECK (source IN ('Cashier', 'Waiter', 'Qr', 'Online', 'Nfc'));

ALTER TABLE orders.order_items DROP CONSTRAINT IF EXISTS order_items_kitchen_state_check;
ALTER TABLE orders.order_items ADD CONSTRAINT order_items_kitchen_state_check
    CHECK (kitchen_state IN ('NotSent', 'Held', 'Sent', 'Preparing', 'Ready', 'Served', 'Cancelled'));
