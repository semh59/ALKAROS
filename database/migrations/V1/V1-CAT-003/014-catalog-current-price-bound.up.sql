ALTER TABLE catalog.products
    ADD CONSTRAINT chk_products_current_price_nonnegative
    CHECK (current_price IS NULL OR current_price >= 0);
