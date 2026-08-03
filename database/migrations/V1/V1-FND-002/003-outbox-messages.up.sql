CREATE TABLE outbox_messages (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type       VARCHAR(100) NOT NULL,
    aggregate_type   VARCHAR(100) NOT NULL,
    aggregate_id     UUID         NOT NULL,
    payload_envelope BYTEA        NOT NULL,
    status           VARCHAR(20)  NOT NULL DEFAULT 'pending'
                     CHECK (status IN ('pending', 'in_flight', 'dispatched', 'dead')),
    attempt_count    INT          NOT NULL DEFAULT 0,
    last_error       TEXT,
    next_retry_at    TIMESTAMPTZ,
    created_at       TIMESTAMPTZ  NOT NULL DEFAULT now(),
    claimed_at       TIMESTAMPTZ,
    dispatched_at    TIMESTAMPTZ
);

CREATE INDEX ix_outbox_messages_claimable
    ON outbox_messages (status, next_retry_at)
    WHERE status IN ('pending', 'in_flight');
