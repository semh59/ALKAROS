-- V12-ONL-001: every authenticated Yemeksepeti webhook delivery, stored once before any
-- processing. event_key identifies one provider event (order, status, provider update time),
-- so a provider retry of the same event lands on the existing row. The raw body carries
-- customer name, phone and address, so it is kept only as an AES-256-GCM envelope; the
-- plain columns are the identifiers processing needs, nothing personal.
CREATE SCHEMA IF NOT EXISTS online_ordering;

CREATE TABLE IF NOT EXISTS online_ordering.yemeksepeti_webhook_inbox (
    inbox_id             UUID          NOT NULL,
    event_key            CHAR(64)      NOT NULL,
    external_order_id    VARCHAR(64)   NOT NULL,
    provider_status      VARCHAR(64)   NOT NULL,
    provider_updated_at  VARCHAR(64)   NULL,
    body_sha256          CHAR(64)      NOT NULL,
    payload_envelope     BYTEA         NOT NULL,
    received_at          TIMESTAMPTZ   NOT NULL DEFAULT now(),
    PRIMARY KEY (inbox_id),
    CONSTRAINT uq_yemeksepeti_webhook_inbox_event UNIQUE (event_key)
);

CREATE INDEX IF NOT EXISTS ix_yemeksepeti_webhook_inbox_order
    ON online_ordering.yemeksepeti_webhook_inbox (external_order_id, received_at);
