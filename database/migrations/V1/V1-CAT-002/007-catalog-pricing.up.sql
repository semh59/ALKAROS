CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE IF NOT EXISTS catalog.product_prices (
    product_price_id  UUID          NOT NULL,
    product_id        UUID          NOT NULL,
    price_type        SMALLINT      NOT NULL CHECK (price_type IN (1)),
    price             NUMERIC(18,2) NOT NULL,
    currency_code     CHAR(3)       NOT NULL DEFAULT 'TRY',
    effective_from    TIMESTAMPTZ   NOT NULL,
    effective_to      TIMESTAMPTZ   NULL,
    PRIMARY KEY (product_price_id),
    CONSTRAINT fk_product_prices_product FOREIGN KEY (product_id) REFERENCES catalog.products (product_id) ON DELETE CASCADE,
    CONSTRAINT excl_product_prices_no_overlap EXCLUDE USING gist (
        product_id WITH =,
        price_type WITH =,
        (currency_code::text) WITH =,
        tstzrange(effective_from, effective_to) WITH &&)
);

CREATE INDEX IF NOT EXISTS ix_product_prices_product ON catalog.product_prices (product_id);