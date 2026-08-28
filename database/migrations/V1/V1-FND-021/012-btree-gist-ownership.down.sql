DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_extension
        WHERE extname = 'btree_gist'
    ) THEN
        RAISE EXCEPTION 'btree_gist must remain installed while catalog.product_prices depends on it';
    END IF;

    IF to_regclass('catalog.product_prices') IS NULL OR NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'catalog.product_prices'::regclass
          AND conname = 'excl_product_prices_no_overlap'
    ) THEN
        RAISE EXCEPTION 'catalog.product_prices exclusion constraint must remain valid through rollback of migration 012';
    END IF;
END
$$;
