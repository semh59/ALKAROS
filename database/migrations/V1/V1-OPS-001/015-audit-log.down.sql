DROP TRIGGER IF EXISTS trg_audit_events_immutable ON audit.audit_events;
DROP FUNCTION IF EXISTS audit.prevent_audit_modification();
DROP TABLE IF EXISTS audit.audit_events;
DROP SCHEMA IF EXISTS audit CASCADE;
