-- The append-only trigger of audit.audit_events is a row trigger, so it never fires for a
-- table-wide clear. A statement-level BEFORE TRUNCATE trigger closes that gap on the parent and on
-- every existing partition (a partition can be cleared directly, and statement triggers are not
-- inherited by partitions created later: call audit.guard_audit_partition(<partition>) for each new one).
-- Dropping an expired whole-year partition (audit-disposal) is a DROP TABLE and stays allowed.
CREATE OR REPLACE FUNCTION audit.prevent_audit_bulk_clear()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'audit_events table is append-only. TRUNCATE is strictly forbidden.';
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION audit.guard_audit_partition(target regclass)
RETURNS void AS $$
BEGIN
    EXECUTE format('DROP TRIGGER IF EXISTS trg_audit_events_no_bulk_clear ON %s', target);
    EXECUTE format(
        'CREATE TRIGGER trg_audit_events_no_bulk_clear BEFORE TRUNCATE ON %s FOR EACH STATEMENT EXECUTE FUNCTION audit.prevent_audit_bulk_clear()',
        target);
END;
$$ LANGUAGE plpgsql;

SELECT audit.guard_audit_partition('audit.audit_events'::regclass);

SELECT audit.guard_audit_partition(i.inhrelid::regclass)
FROM pg_inherits i
WHERE i.inhparent = 'audit.audit_events'::regclass;
