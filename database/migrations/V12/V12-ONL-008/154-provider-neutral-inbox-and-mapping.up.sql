-- V12-ONL-008: the webhook inbox and the product mappings belong to every online platform, not only Yemeksepeti. Each
-- row names its platform; uniqueness is per platform. Existing rows came from Yemeksepeti. Each table is guarded,
-- because some module test databases load only one of them.
DO $$ BEGIN
    IF to_regclass('online_ordering.yemeksepeti_webhook_inbox') IS NOT NULL THEN
        ALTER TABLE online_ordering.yemeksepeti_webhook_inbox RENAME TO provider_inbox;
        ALTER TABLE online_ordering.provider_inbox RENAME CONSTRAINT yemeksepeti_webhook_inbox_pkey TO provider_inbox_pkey;
        ALTER TABLE online_ordering.provider_inbox
            ADD COLUMN provider TEXT NOT NULL DEFAULT 'yemeksepeti'
                CONSTRAINT ck_provider_inbox_provider CHECK (provider ~ '^[a-z][a-z0-9-]{1,31}$');
        ALTER TABLE online_ordering.provider_inbox ALTER COLUMN provider DROP DEFAULT;
        ALTER TABLE online_ordering.provider_inbox DROP CONSTRAINT uq_yemeksepeti_webhook_inbox_event;
        ALTER TABLE online_ordering.provider_inbox ADD CONSTRAINT uq_provider_inbox_event UNIQUE (provider, event_key);
        DROP INDEX IF EXISTS online_ordering.ix_yemeksepeti_webhook_inbox_order;
        CREATE INDEX ix_provider_inbox_order ON online_ordering.provider_inbox (provider, external_order_id, received_at);
    END IF;
    IF to_regclass('online_ordering.provider_inbox') IS NOT NULL
       AND EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_yemeksepeti_webhook_inbox_processed') THEN
        ALTER TABLE online_ordering.provider_inbox
            RENAME CONSTRAINT ck_yemeksepeti_webhook_inbox_processed TO ck_provider_inbox_processed;
    END IF;
    IF to_regclass('online_ordering.ix_yemeksepeti_webhook_inbox_pending') IS NOT NULL THEN
        ALTER INDEX online_ordering.ix_yemeksepeti_webhook_inbox_pending RENAME TO ix_provider_inbox_pending;
    END IF;

    IF to_regclass('online_ordering.yemeksepeti_product_mappings') IS NOT NULL THEN
        ALTER TABLE online_ordering.yemeksepeti_product_mappings RENAME TO provider_product_mappings;
        ALTER TABLE online_ordering.provider_product_mappings
            RENAME CONSTRAINT yemeksepeti_product_mappings_pkey TO provider_product_mappings_pkey;
        ALTER TABLE online_ordering.provider_product_mappings
            RENAME CONSTRAINT ck_yemeksepeti_product_mappings_range TO ck_provider_product_mappings_range;
        ALTER TABLE online_ordering.provider_product_mappings
            RENAME CONSTRAINT ck_yemeksepeti_product_mappings_sku_not_blank TO ck_provider_product_mappings_sku_not_blank;
        ALTER TABLE online_ordering.provider_product_mappings
            ADD COLUMN provider TEXT NOT NULL DEFAULT 'yemeksepeti'
                CONSTRAINT ck_provider_product_mappings_provider CHECK (provider ~ '^[a-z][a-z0-9-]{1,31}$');
        ALTER TABLE online_ordering.provider_product_mappings ALTER COLUMN provider DROP DEFAULT;
        DROP INDEX IF EXISTS online_ordering.uq_yemeksepeti_product_mappings_open_sku;
        DROP INDEX IF EXISTS online_ordering.uq_yemeksepeti_product_mappings_open_product;
        DROP INDEX IF EXISTS online_ordering.ix_yemeksepeti_product_mappings_sku_range;
        -- One open mapping per platform SKU, and a product is published under one SKU per platform at a time.
        CREATE UNIQUE INDEX uq_provider_product_mappings_open_sku
            ON online_ordering.provider_product_mappings (provider, external_sku) WHERE effective_to IS NULL;
        CREATE UNIQUE INDEX uq_provider_product_mappings_open_product
            ON online_ordering.provider_product_mappings (provider, product_id) WHERE effective_to IS NULL;
        CREATE INDEX ix_provider_product_mappings_sku_range
            ON online_ordering.provider_product_mappings (provider, external_sku, effective_from);
    END IF;
END $$;
