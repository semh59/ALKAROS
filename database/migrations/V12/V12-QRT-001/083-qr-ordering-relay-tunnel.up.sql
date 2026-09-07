-- Idempotent, same self-containedness reasoning as 082: relay_tunnel has no
-- FK into table_mgmt, so it does not otherwise need 078 at all.
CREATE SCHEMA IF NOT EXISTS qr_ordering;

-- Single well-known row (tunnel_key = 'cloudflare'), mirroring
-- relay_credentials/relay_provider_config: the tunnel run-token is a
-- bearer credential for that tunnel, so it is stored as an encrypted
-- envelope, never plaintext.
CREATE TABLE IF NOT EXISTS qr_ordering.relay_tunnel (
    tunnel_key      TEXT        NOT NULL,
    tunnel_id       TEXT        NOT NULL,
    hostname        TEXT        NOT NULL,
    token_envelope  BYTEA       NOT NULL,
    updated_at      TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (tunnel_key)
);
