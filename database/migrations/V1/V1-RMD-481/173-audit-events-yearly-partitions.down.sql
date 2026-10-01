DO $$
DECLARE
    copied bigint;
    original bigint;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'audit' AND c.relname = 'audit_events' AND c.relkind = 'p') THEN
        RETURN;
    END IF;

    SELECT count(*) INTO original FROM audit.audit_events;
    CREATE TABLE audit.audit_events_plain (LIKE audit.audit_events INCLUDING DEFAULTS);
    INSERT INTO audit.audit_events_plain SELECT * FROM audit.audit_events;
    SELECT count(*) INTO copied FROM audit.audit_events_plain;
    IF copied <> original THEN
        RAISE EXCEPTION 'audit_events copy lost rows: % of %', copied, original;
    END IF;
    DROP TABLE audit.audit_events;
    ALTER TABLE audit.audit_events_plain RENAME TO audit_events;
    ALTER TABLE audit.audit_events ADD CONSTRAINT audit_events_pkey PRIMARY KEY (id);

    CREATE INDEX ix_audit_events_aggregate ON audit.audit_events (aggregate_type, aggregate_id, occurred_at);
    CREATE INDEX ix_audit_events_correlation ON audit.audit_events (correlation_id);
    CREATE INDEX ix_audit_events_occurred_at ON audit.audit_events (occurred_at);

    CREATE TRIGGER trg_audit_events_immutable
    BEFORE UPDATE OR DELETE ON audit.audit_events
    FOR EACH ROW EXECUTE FUNCTION audit.prevent_audit_modification();
END
$$;
