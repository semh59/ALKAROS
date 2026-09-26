-- V12-ONL-008: back to the Yemeksepeti-only tables. Rows of any other platform would have nowhere to go, so the
-- rollback stops with an explicit error instead of dropping them.
DO $$
DECLARE
    other_platform BOOLEAN := false;
BEGIN
    -- Each table is checked on its own (dynamic SQL): a database may hold only one of them.
    IF to_regclass('online_ordering.provider_inbox') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM online_ordering.provider_inbox WHERE provider <> ''yemeksepeti'')'
            INTO other_platform;
    END IF;
    IF NOT other_platform AND to_regclass('online_ordering.provider_product_mappings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM online_ordering.provider_product_mappings WHERE provider <> ''yemeksepeti'')'
            INTO other_platform;
    END IF;
    IF other_platform THEN
        RAISE EXCEPTION 'V12-ONL-008 rollback refused: rows of another online platform exist';
    END IF;

    IF to_regclass('online_ordering.provider_inbox') IS NOT NULL THEN
        ALTER TABLE online_ordering.provider_inbox DROP CONSTRAINT uq_provider_inbox_event;
        ALTER TABLE online_ordering.provider_inbox ADD CONSTRAINT uq_yemeksepeti_webhook_inbox_event UNIQUE (event_key);
        DROP INDEX IF EXISTS online_ordering.ix_provider_inbox_order;
        CREATE INDEX ix_yemeksepeti_webhook_inbox_order ON online_ordering.provider_inbox (external_order_id, received_at);
        ALTER TABLE online_ordering.provider_inbox DROP COLUMN provider;
        IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_provider_inbox_processed') THEN
            ALTER TABLE online_ordering.provider_inbox
                RENAME CONSTRAINT ck_provider_inbox_processed TO ck_yemeksepeti_webhook_inbox_processed;
        END IF;
        IF to_regclass('online_ordering.ix_provider_inbox_pending') IS NOT NULL THEN
            ALTER INDEX online_ordering.ix_provider_inbox_pending RENAME TO ix_yemeksepeti_webhook_inbox_pending;
        END IF;
        ALTER TABLE online_ordering.provider_inbox RENAME CONSTRAINT provider_inbox_pkey TO yemeksepeti_webhook_inbox_pkey;
        ALTER TABLE online_ordering.provider_inbox RENAME TO yemeksepeti_webhook_inbox;
    END IF;

    IF to_regclass('online_ordering.provider_product_mappings') IS NOT NULL THEN
        DROP INDEX IF EXISTS online_ordering.uq_provider_product_mappings_open_sku;
        DROP INDEX IF EXISTS online_ordering.uq_provider_product_mappings_open_product;
        DROP INDEX IF EXISTS online_ordering.ix_provider_product_mappings_sku_range;
        ALTER TABLE online_ordering.provider_product_mappings DROP COLUMN provider;
        CREATE UNIQUE INDEX uq_yemeksepeti_product_mappings_open_sku
            ON online_ordering.provider_product_mappings (external_sku) WHERE effective_to IS NULL;
        CREATE UNIQUE INDEX uq_yemeksepeti_product_mappings_open_product
            ON online_ordering.provider_product_mappings (product_id) WHERE effective_to IS NULL;
        CREATE INDEX ix_yemeksepeti_product_mappings_sku_range
            ON online_ordering.provider_product_mappings (external_sku, effective_from);
        ALTER TABLE online_ordering.provider_product_mappings
            RENAME CONSTRAINT ck_provider_product_mappings_sku_not_blank TO ck_yemeksepeti_product_mappings_sku_not_blank;
        ALTER TABLE online_ordering.provider_product_mappings
            RENAME CONSTRAINT ck_provider_product_mappings_range TO ck_yemeksepeti_product_mappings_range;
        ALTER TABLE online_ordering.provider_product_mappings
            RENAME CONSTRAINT provider_product_mappings_pkey TO yemeksepeti_product_mappings_pkey;
        ALTER TABLE online_ordering.provider_product_mappings RENAME TO yemeksepeti_product_mappings;
    END IF;
END $$;
