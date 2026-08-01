CREATE TABLE IF NOT EXISTS idempotency_keys (
    client_id         VARCHAR(100) NOT NULL,
    operation_id      VARCHAR(100) NOT NULL,
    request_hash      CHAR(64)     NOT NULL,
    response_envelope BYTEA        NOT NULL,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT now(),
    expires_at        TIMESTAMPTZ  NOT NULL,
    PRIMARY KEY (client_id, operation_id)
);
