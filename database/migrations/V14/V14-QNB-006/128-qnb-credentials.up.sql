CREATE SCHEMA IF NOT EXISTS invoicing;

CREATE TABLE IF NOT EXISTS invoicing.qnb_credentials (
    credential_key      TEXT          NOT NULL,
    user_id              TEXT          NOT NULL,
    vergi_tc_kimlik_no   TEXT          NOT NULL,
    envelope_bytes       BYTEA         NOT NULL,
    updated_at           TIMESTAMPTZ   NOT NULL,
    updated_by           UUID          NULL,
    PRIMARY KEY (credential_key)
);
