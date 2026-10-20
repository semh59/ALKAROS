-- Removes the table-wide clear guard (the first drop cascades to every trigger that uses it).
DROP FUNCTION IF EXISTS audit.prevent_audit_bulk_clear() CASCADE;
DROP FUNCTION IF EXISTS audit.guard_audit_partition(regclass);
