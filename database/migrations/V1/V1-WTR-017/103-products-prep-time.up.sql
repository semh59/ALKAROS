ALTER TABLE catalog.products
    ADD COLUMN IF NOT EXISTS prep_time_minutes INTEGER NULL;

ALTER TABLE catalog.products
    ADD CONSTRAINT products_prep_time_minutes_range
    CHECK (prep_time_minutes IS NULL OR prep_time_minutes BETWEEN 1 AND 180);
