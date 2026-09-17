-- V12-QRT-005: the relay connector now runs in its own container, separate
-- from `api` — RelayConnectorSupervisor's live status can no longer be read
-- from an in-process singleton by the api's own RelaySettings status
-- endpoint. Single well-known row (connector_key = 'cloudflare'), same
-- pattern as relay_tunnel (083): the connector container writes its own
-- current state here on every change, api reads it back.
--
-- Idempotent, same self-containedness reasoning as 082/083: this table has
-- no FK into anything else qr_ordering owns, so this migration does not
-- otherwise need 083 at all.
CREATE SCHEMA IF NOT EXISTS qr_ordering;

CREATE TABLE IF NOT EXISTS qr_ordering.relay_connector_status (
    connector_key      TEXT        NOT NULL,
    state               TEXT        NOT NULL CHECK (state IN ('NotConfigured', 'Running', 'Restarting')),
    last_started_at     TIMESTAMPTZ NULL,
    restart_count        INT         NOT NULL DEFAULT 0,
    last_exit_code       INT         NULL,
    updated_at           TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (connector_key)
);
