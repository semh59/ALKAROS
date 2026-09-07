-- Idempotent: V12-QRS-001 (078) already creates this schema in the real
-- migration sequence. Repeated here so this migration is self-contained
-- for isolated testing (relay_provider_config has no FK into table_mgmt,
-- unlike table_tokens, so it does not otherwise need 078 at all).
CREATE SCHEMA IF NOT EXISTS qr_ordering;

CREATE TABLE IF NOT EXISTS qr_ordering.relay_provider_config (
    config_key   TEXT        NOT NULL,
    account_id   TEXT        NOT NULL,
    zone_id      TEXT        NOT NULL,
    base_domain  TEXT        NOT NULL,
    updated_at   TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (config_key)
);
