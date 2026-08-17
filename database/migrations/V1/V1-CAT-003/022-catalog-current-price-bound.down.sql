ALTER TABLE catalog.products
    DROP CONSTRAINT IF EXISTS chk_products_current_price_nonnegative;
