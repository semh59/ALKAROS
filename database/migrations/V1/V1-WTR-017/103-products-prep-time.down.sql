ALTER TABLE catalog.products
    DROP CONSTRAINT IF EXISTS products_prep_time_minutes_range;

ALTER TABLE catalog.products
    DROP COLUMN IF EXISTS prep_time_minutes;
