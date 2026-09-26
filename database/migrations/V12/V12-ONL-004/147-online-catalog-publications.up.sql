-- V12-ONL-004: every catalog publication to an online channel and exactly what it contained.
-- A publication is written together with its outbox request, so it is delivered exactly when it
-- committed; its content hash lets an identical republish be recorded as Unchanged without
-- calling the provider again.
CREATE SCHEMA IF NOT EXISTS online_ordering;

CREATE TABLE IF NOT EXISTS online_ordering.catalog_publications (
    publication_id            UUID          NOT NULL,
    channel                   VARCHAR(40)   NOT NULL,
    menu_id                   UUID          NOT NULL,
    status                    VARCHAR(20)   NOT NULL
        CHECK (status IN ('Pending', 'Delivered', 'Unchanged', 'NothingToPublish')),
    content_sha256            CHAR(64)      NOT NULL,
    item_count                INT           NOT NULL CHECK (item_count >= 0),
    validation_errors         JSONB         NOT NULL DEFAULT '[]'::jsonb,
    unsupported_capabilities  JSONB         NOT NULL DEFAULT '[]'::jsonb,
    requested_by              UUID          NOT NULL,
    requested_at              TIMESTAMPTZ   NOT NULL DEFAULT now(),
    provider_job_id           VARCHAR(100)  NULL,
    delivered_at              TIMESTAMPTZ   NULL,
    delivery_attempts         INT           NOT NULL DEFAULT 0,
    last_error                VARCHAR(200)  NULL,
    PRIMARY KEY (publication_id)
);

CREATE INDEX IF NOT EXISTS ix_catalog_publications_channel_menu
    ON online_ordering.catalog_publications (channel, menu_id, requested_at DESC);

CREATE TABLE IF NOT EXISTS online_ordering.catalog_publication_items (
    publication_id  UUID           NOT NULL REFERENCES online_ordering.catalog_publications (publication_id) ON DELETE CASCADE,
    product_id      UUID           NOT NULL,
    external_sku    VARCHAR(100)   NOT NULL,
    title           VARCHAR(300)   NOT NULL,
    price           NUMERIC(18,2)  NOT NULL CHECK (price >= 0),
    active          BOOLEAN        NOT NULL,
    PRIMARY KEY (publication_id, product_id),
    CONSTRAINT uq_catalog_publication_items_sku UNIQUE (publication_id, external_sku)
);
