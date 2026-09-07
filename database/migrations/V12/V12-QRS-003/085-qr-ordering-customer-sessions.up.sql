-- Idempotent, same self-containedness reasoning as 078: repeats the schema
-- create here so this migration is self-contained for isolated testing.
CREATE SCHEMA IF NOT EXISTS qr_ordering;

CREATE TABLE IF NOT EXISTS qr_ordering.customer_sessions (
    session_id         UUID          NOT NULL,
    table_id           UUID          NOT NULL,
    token_hash         TEXT          NOT NULL,
    issued_at          TIMESTAMPTZ   NOT NULL,
    last_activity_at   TIMESTAMPTZ   NOT NULL,
    expires_at         TIMESTAMPTZ   NOT NULL,
    revoked_at         TIMESTAMPTZ   NULL,
    revoked_reason     TEXT          NULL,
    PRIMARY KEY (session_id),
    UNIQUE (token_hash),
    CONSTRAINT fk_customer_sessions_table FOREIGN KEY (table_id) REFERENCES table_mgmt.tables (table_id),
    CONSTRAINT ck_customer_sessions_revocation_pair CHECK ((revoked_at IS NULL) = (revoked_reason IS NULL))
);

-- Unlike qr_ordering.table_tokens (one active token per table — the QR code
-- itself is shared), several customers at the same table each get their own
-- session, so there is deliberately no "one active session per table"
-- constraint here.
CREATE INDEX IF NOT EXISTS ix_customer_sessions_table ON qr_ordering.customer_sessions (table_id);
