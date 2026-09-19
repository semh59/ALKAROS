CREATE SCHEMA IF NOT EXISTS payments;

CREATE TABLE IF NOT EXISTS payments.token_terminal_credentials (
    credential_key  TEXT          NOT NULL,
    merchant_id     TEXT          NOT NULL,
    branch_id       TEXT          NOT NULL,
    terminal_id     TEXT          NOT NULL,
    client_id       TEXT          NOT NULL,
    envelope_bytes  BYTEA         NOT NULL,
    updated_at      TIMESTAMPTZ   NOT NULL,
    updated_by      UUID          NULL,
    PRIMARY KEY (credential_key)
);
