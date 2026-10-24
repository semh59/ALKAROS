-- Runs before the 105 and 077 down scripts. Rolling back loses the distinction:
-- a self-ordered NFC order becomes a QR order (same customer-facing flow) and a
-- held course item becomes a sent one (it was already printed on the ticket).
ALTER TABLE orders.orders DROP CONSTRAINT IF EXISTS orders_source_check;
ALTER TABLE orders.order_items DROP CONSTRAINT IF EXISTS order_items_kitchen_state_check;

UPDATE orders.orders SET source = 'Qr' WHERE source = 'Nfc';
UPDATE orders.order_items SET kitchen_state = 'Sent' WHERE kitchen_state = 'Held';

ALTER TABLE orders.orders ADD CONSTRAINT orders_source_check
    CHECK (source IN ('Cashier', 'Waiter', 'Qr', 'Online', 'Nfc'));
ALTER TABLE orders.order_items ADD CONSTRAINT order_items_kitchen_state_check
    CHECK (kitchen_state IN ('NotSent', 'Held', 'Sent', 'Preparing', 'Ready', 'Served', 'Cancelled'));
