ALTER TABLE catalog.products
    ADD COLUMN IF NOT EXISTS is_available BOOLEAN NOT NULL DEFAULT TRUE;

CREATE INDEX IF NOT EXISTS ix_products_available
    ON catalog.products (is_available) WHERE is_available;
