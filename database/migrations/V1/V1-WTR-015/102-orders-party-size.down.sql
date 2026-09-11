-- Reverse of 102.
ALTER TABLE orders.orders DROP CONSTRAINT IF EXISTS chk_orders_party_size;
ALTER TABLE orders.orders DROP COLUMN IF EXISTS party_size;
