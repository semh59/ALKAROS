CREATE SCHEMA IF NOT EXISTS audit;

CREATE TABLE IF NOT EXISTS audit.audit_events (
    id                  UUID         PRIMARY KEY,
    event_name          VARCHAR(128) NOT NULL,
    aggregate_type      VARCHAR(64)  NOT NULL,
    aggregate_id        UUID         NOT NULL,
    actor_id            UUID         NULL,
    actor_type          VARCHAR(32)  NOT NULL,
    reason              TEXT         NULL,
    correlation_id      VARCHAR(128) NOT NULL,
    causation_id        VARCHAR(128) NULL,
    before_state_json   JSONB        NULL,
    after_state_json    JSONB        NULL,
    metadata_json       JSONB        NULL,
    occurred_at         TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_audit_events_aggregate
    ON audit.audit_events (aggregate_type, aggregate_id, occurred_at);

CREATE INDEX IF NOT EXISTS ix_audit_events_correlation
    ON audit.audit_events (correlation_id);

CREATE INDEX IF NOT EXISTS ix_audit_events_occurred_at
    ON audit.audit_events (occurred_at);

-- Invariant (PDF:II.9 / PDF:III.24): audit_events table is strictly append-only.
-- Updates and Deletions must fail closed at the database engine level.
CREATE OR REPLACE FUNCTION audit.prevent_audit_modification()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'audit_events table is append-only. UPDATE and DELETE operations are strictly forbidden.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_audit_events_immutable ON audit.audit_events;

CREATE TRIGGER trg_audit_events_immutable
BEFORE UPDATE OR DELETE ON audit.audit_events
FOR EACH ROW EXECUTE FUNCTION audit.prevent_audit_modification();
