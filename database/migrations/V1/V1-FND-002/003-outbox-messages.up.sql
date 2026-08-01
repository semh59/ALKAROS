CREATE TABLE IF NOT EXISTS outbox_messages (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type       VARCHAR(100) NOT NULL,
    aggregate_type   VARCHAR(100) NOT NULL,
    aggregate_id     UUID         NOT NULL,
    payload_envelope BYTEA        NOT NULL,
    status           VARCHAR(20)  NOT NULL DEFAULT 'pending'
                     CHECK (status IN ('pending', 'dispatched', 'dead')),
    attempt_count    INT          NOT NULL DEFAULT 0,
    last_error       TEXT,
    next_retry_at    TIMESTAMPTZ,
    created_at       TIMESTAMPTZ  NOT NULL DEFAULT now(),
    dispatched_at    TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_outbox_messages_pending
    ON outbox_messages (status, next_retry_at)
    WHERE status = 'pending';
