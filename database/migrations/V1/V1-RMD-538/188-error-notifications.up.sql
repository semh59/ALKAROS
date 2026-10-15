-- Monitoring notifications: a durable outgoing queue and per-fingerprint suppression windows. Rows carry
-- fixed tokens only (category, count, time, terminal label); no error text, customer or amount data.
CREATE SCHEMA IF NOT EXISTS observability;

CREATE TABLE IF NOT EXISTS observability.notification_queue (
    id               BIGSERIAL     NOT NULL,
    fingerprint      TEXT          NOT NULL,
    layer            TEXT          NOT NULL,
    category         TEXT          NOT NULL,
    severity         TEXT          NOT NULL,
    terminal_label   TEXT          NULL,
    kind             TEXT          NOT NULL,
    state            TEXT          NOT NULL DEFAULT 'Pending',
    attempts         INTEGER       NOT NULL DEFAULT 0,
    enqueued_at      TIMESTAMPTZ   NOT NULL,
    next_attempt_at  TIMESTAMPTZ   NOT NULL,
    sent_at          TIMESTAMPTZ   NULL,
    PRIMARY KEY (id),
    CONSTRAINT ck_notification_queue_kind CHECK (kind IN ('Critical', 'Digest')),
    CONSTRAINT ck_notification_queue_state CHECK (state IN ('Pending', 'Sent', 'Suppressed', 'Failed', 'Digested')),
    CONSTRAINT ck_notification_queue_attempts CHECK (attempts >= 0)
);

CREATE INDEX IF NOT EXISTS ix_notification_queue_due ON observability.notification_queue (next_attempt_at) WHERE state = 'Pending';
CREATE INDEX IF NOT EXISTS ix_notification_queue_enqueued ON observability.notification_queue (enqueued_at);

CREATE TABLE IF NOT EXISTS observability.notification_suppressions (
    fingerprint   TEXT         NOT NULL,
    last_sent_at  TIMESTAMPTZ  NOT NULL,
    PRIMARY KEY (fingerprint)
);
