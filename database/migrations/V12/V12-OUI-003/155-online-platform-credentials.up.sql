-- V12-OUI-003: each online platform's API settings, entered by a manager from the interface.
-- Non-secret settings (base URL, chain/vendor/supplier ids) are plaintext; every secret field lives
-- only inside envelope_bytes, an AES-256-GCM envelope sealed with the envelope master key.
CREATE SCHEMA IF NOT EXISTS online_ordering;

CREATE TABLE IF NOT EXISTS online_ordering.platform_credentials (
    provider        TEXT         NOT NULL,
    plain_fields    JSONB        NOT NULL DEFAULT '{}'::jsonb,
    secret_fields   TEXT[]       NOT NULL DEFAULT '{}',
    envelope_bytes  BYTEA        NULL,
    updated_at      TIMESTAMPTZ  NOT NULL,
    updated_by      UUID         NULL,
    CONSTRAINT pk_platform_credentials PRIMARY KEY (provider),
    CONSTRAINT ck_platform_credentials_provider CHECK (provider ~ '^[a-z][a-z0-9-]{0,31}$'),
    CONSTRAINT ck_platform_credentials_plain_object CHECK (jsonb_typeof(plain_fields) = 'object'),
    -- A secret field is listed exactly when the envelope holds it.
    CONSTRAINT ck_platform_credentials_envelope CHECK (
        (cardinality(secret_fields) = 0) = (envelope_bytes IS NULL))
);
