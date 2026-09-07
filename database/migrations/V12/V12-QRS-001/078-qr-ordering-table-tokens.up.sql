CREATE SCHEMA IF NOT EXISTS qr_ordering;

CREATE TABLE IF NOT EXISTS qr_ordering.table_tokens (
    token_id        UUID          NOT NULL,
    table_id        UUID          NOT NULL,
    token_hash      TEXT          NOT NULL,
    issued_at       TIMESTAMPTZ   NOT NULL,
    expires_at      TIMESTAMPTZ   NOT NULL,
    revoked_at      TIMESTAMPTZ   NULL,
    revoked_reason  TEXT          NULL,
    PRIMARY KEY (token_id),
    UNIQUE (token_hash),
    CONSTRAINT fk_table_tokens_table FOREIGN KEY (table_id) REFERENCES table_mgmt.tables (table_id),
    CONSTRAINT ck_table_tokens_revocation_pair CHECK ((revoked_at IS NULL) = (revoked_reason IS NULL))
);

-- At most one non-revoked token per table at any instant (V12-QRS-001):
-- Issue requires none exist yet, Rotate atomically revokes-then-inserts in
-- one transaction, so this constraint is never raced against in a way the
-- application does not already expect and translate into a clean error.
CREATE UNIQUE INDEX IF NOT EXISTS ux_table_tokens_active_per_table
    ON qr_ordering.table_tokens (table_id) WHERE revoked_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_table_tokens_table ON qr_ordering.table_tokens (table_id);
