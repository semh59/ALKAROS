CREATE TABLE IF NOT EXISTS inbox_messages (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    source            VARCHAR(100) NOT NULL,
    external_event_id VARCHAR(200) NOT NULL,
    payload_envelope  BYTEA        NOT NULL,
    status            VARCHAR(20)  NOT NULL DEFAULT 'pending'
                      CHECK (status IN ('pending', 'processed', 'dead')),
    attempt_count     INT          NOT NULL DEFAULT 0,
    last_error        TEXT,
    next_retry_at     TIMESTAMPTZ,
    received_at       TIMESTAMPTZ  NOT NULL DEFAULT now(),
    processed_at      TIMESTAMPTZ,
    CONSTRAINT uq_inbox_messages_source_event UNIQUE (source, external_event_id)
);
