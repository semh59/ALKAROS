-- V12-MAP-001: which active catalog product a Yemeksepeti SKU stands for, over time.
-- A mapping is open while effective_to is NULL; at most one open mapping per SKU and per
-- product, so every provider SKU resolves to one product and every product publishes
-- under one SKU. Closed ranges keep history for orders placed while they were in effect.
CREATE SCHEMA IF NOT EXISTS online_ordering;

CREATE TABLE IF NOT EXISTS online_ordering.yemeksepeti_product_mappings (
    mapping_id      UUID          NOT NULL,
    external_sku    VARCHAR(100)  NOT NULL,
    product_id      UUID          NOT NULL,
    effective_from  TIMESTAMPTZ   NOT NULL,
    effective_to    TIMESTAMPTZ   NULL,
    created_by      UUID          NOT NULL,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    closed_by       UUID          NULL,
    PRIMARY KEY (mapping_id),
    CONSTRAINT ck_yemeksepeti_product_mappings_range
        CHECK (effective_to IS NULL OR effective_to > effective_from),
    CONSTRAINT ck_yemeksepeti_product_mappings_sku_not_blank
        CHECK (length(btrim(external_sku)) > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_yemeksepeti_product_mappings_open_sku
    ON online_ordering.yemeksepeti_product_mappings (external_sku)
    WHERE effective_to IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_yemeksepeti_product_mappings_open_product
    ON online_ordering.yemeksepeti_product_mappings (product_id)
    WHERE effective_to IS NULL;

CREATE INDEX IF NOT EXISTS ix_yemeksepeti_product_mappings_sku_range
    ON online_ordering.yemeksepeti_product_mappings (external_sku, effective_from);
