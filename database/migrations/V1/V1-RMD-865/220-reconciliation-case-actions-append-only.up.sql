-- The case history is an audit trail: a row is never changed or removed, and the table is never cleared in bulk.
-- The row trigger covers UPDATE and DELETE; a bulk clear needs its own statement trigger because row triggers never
-- fire for it.
CREATE OR REPLACE FUNCTION reconciliation.case_actions_guard() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'case_actions is append-only' USING ERRCODE = 'integrity_constraint_violation';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_case_actions_guard ON reconciliation.case_actions;
CREATE TRIGGER trg_case_actions_guard
    BEFORE UPDATE OR DELETE ON reconciliation.case_actions
    FOR EACH ROW EXECUTE FUNCTION reconciliation.case_actions_guard();

DROP TRIGGER IF EXISTS trg_case_actions_clear_guard ON reconciliation.case_actions;
CREATE TRIGGER trg_case_actions_clear_guard
    BEFORE TRUNCATE ON reconciliation.case_actions
    FOR EACH STATEMENT EXECUTE FUNCTION reconciliation.case_actions_guard();
