-- audit.audit_events becomes a table partitioned by occurred_at year so a whole year can be dropped after its retention.
-- Uniqueness of id is now per (id, occurred_at); the table has no foreign keys and ids are random UUIDs.
-- A DEFAULT partition catches any date outside the pre-created years so an audit insert never fails; it is never dropped.
DO $$
DECLARE
    first_year integer;
    copied bigint;
    original bigint;
    y integer;
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'audit' AND c.relname = 'audit_events' AND c.relkind = 'p') THEN
        RETURN;
    END IF;

    SELECT count(*) INTO original FROM audit.audit_events;
    SELECT least(2020, coalesce(extract(year FROM min(occurred_at AT TIME ZONE 'UTC'))::integer, 2020))
      INTO first_year FROM audit.audit_events;

    ALTER TABLE audit.audit_events RENAME TO audit_events_unpartitioned;
    ALTER TABLE audit.audit_events_unpartitioned DROP CONSTRAINT audit_events_pkey;
    DROP INDEX IF EXISTS audit.ix_audit_events_aggregate;
    DROP INDEX IF EXISTS audit.ix_audit_events_correlation;
    DROP INDEX IF EXISTS audit.ix_audit_events_occurred_at;

    CREATE TABLE audit.audit_events (
        id                  UUID         NOT NULL,
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
        occurred_at         TIMESTAMPTZ  NOT NULL DEFAULT now(),
        PRIMARY KEY (id, occurred_at)
    ) PARTITION BY RANGE (occurred_at);

    FOR y IN first_year..2060 LOOP
        EXECUTE format(
            'CREATE TABLE audit.audit_events_y%s PARTITION OF audit.audit_events FOR VALUES FROM (%L) TO (%L)',
            y, y || '-01-01 00:00:00+00', (y + 1) || '-01-01 00:00:00+00');
    END LOOP;
    CREATE TABLE audit.audit_events_default PARTITION OF audit.audit_events DEFAULT;

    INSERT INTO audit.audit_events SELECT * FROM audit.audit_events_unpartitioned;
    SELECT count(*) INTO copied FROM audit.audit_events;
    IF copied <> original THEN
        RAISE EXCEPTION 'audit_events copy lost rows: % of %', copied, original;
    END IF;
    DROP TABLE audit.audit_events_unpartitioned;

    CREATE INDEX ix_audit_events_aggregate ON audit.audit_events (aggregate_type, aggregate_id, occurred_at);
    CREATE INDEX ix_audit_events_correlation ON audit.audit_events (correlation_id);
    CREATE INDEX ix_audit_events_occurred_at ON audit.audit_events (occurred_at);

    CREATE TRIGGER trg_audit_events_immutable
    BEFORE UPDATE OR DELETE ON audit.audit_events
    FOR EACH ROW EXECUTE FUNCTION audit.prevent_audit_modification();
END
$$;
