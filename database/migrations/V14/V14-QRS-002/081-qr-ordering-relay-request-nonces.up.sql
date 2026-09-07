CREATE TABLE IF NOT EXISTS qr_ordering.relay_request_nonces (
    token_id  UUID        NOT NULL,
    nonce     UUID        NOT NULL,
    used_at   TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (token_id, nonce),
    CONSTRAINT fk_relay_request_nonces_token FOREIGN KEY (token_id) REFERENCES qr_ordering.table_tokens (token_id)
);

-- A nonce only matters while its token could still be valid (V14-QRS-001's
-- 4-hour lifetime); this index lets a periodic cleanup job purge rows for
-- long-expired tokens without a full table scan. No such job exists yet —
-- out of this migration's scope — the index is cheap to have ready.
CREATE INDEX IF NOT EXISTS ix_relay_request_nonces_used_at ON qr_ordering.relay_request_nonces (used_at);
