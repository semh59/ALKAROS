CREATE TABLE IF NOT EXISTS qr_ordering.relay_credentials (
    credential_key  TEXT          NOT NULL,
    envelope_bytes  BYTEA         NOT NULL,
    updated_at      TIMESTAMPTZ   NOT NULL,
    updated_by      UUID          NULL,
    PRIMARY KEY (credential_key)
);
