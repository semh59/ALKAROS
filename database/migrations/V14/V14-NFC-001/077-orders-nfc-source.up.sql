ALTER TABLE orders.orders DROP CONSTRAINT orders_source_check;
ALTER TABLE orders.orders ADD CONSTRAINT orders_source_check
    CHECK (source IN ('Cashier', 'Waiter', 'Qr', 'Online', 'Nfc'));
