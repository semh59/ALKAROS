-- V12-ONL-005: what each online channel should show for a product's availability and what it
-- was last told. Every state carries the source version it was observed at (the sum of the
-- product's stock-balance row versions, which only ever grows), so an older observation or an
-- older delivery can never overwrite a newer one.
CREATE SCHEMA IF NOT EXISTS online_ordering;

CREATE TABLE IF NOT EXISTS online_ordering.availability_states (
    channel             VARCHAR(40)   NOT NULL,
    product_id          UUID          NOT NULL,
    external_sku        VARCHAR(100)  NOT NULL,
    desired_quantity    INT           NOT NULL CHECK (desired_quantity >= 0),
    desired_version     BIGINT        NOT NULL,
    desired_at          TIMESTAMPTZ   NOT NULL DEFAULT now(),
    delivered_quantity  INT           NULL CHECK (delivered_quantity IS NULL OR delivered_quantity >= 0),
    delivered_version   BIGINT        NULL,
    delivered_at        TIMESTAMPTZ   NULL,
    delivery_attempts   INT           NOT NULL DEFAULT 0,
    last_error          VARCHAR(200)  NULL,
    PRIMARY KEY (channel, product_id)
);

CREATE INDEX IF NOT EXISTS ix_availability_states_pending
    ON online_ordering.availability_states (channel, desired_at)
    WHERE delivered_quantity IS DISTINCT FROM desired_quantity;
