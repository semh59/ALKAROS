-- Only a product marked as allowing half portions may be ordered or refunded in half-unit steps; every other product is sold in whole units.
ALTER TABLE catalog.products
    ADD COLUMN IF NOT EXISTS allows_half_portion BOOLEAN NOT NULL DEFAULT FALSE;

-- A product that was already sold in fractions keeps selling that way: the flag starts on for it.
DO $$
BEGIN
    IF to_regclass('orders.order_items') IS NOT NULL THEN
        UPDATE catalog.products
        SET allows_half_portion = TRUE
        WHERE product_id IN (SELECT DISTINCT product_id FROM orders.order_items WHERE quantity <> trunc(quantity));
    END IF;
END
$$;
