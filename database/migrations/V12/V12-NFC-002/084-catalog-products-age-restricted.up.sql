ALTER TABLE catalog.products
    ADD COLUMN is_age_restricted BOOLEAN NOT NULL DEFAULT false;
