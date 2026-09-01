DROP INDEX IF EXISTS catalog.ix_products_available;

ALTER TABLE catalog.products
    DROP COLUMN IF EXISTS is_available;
