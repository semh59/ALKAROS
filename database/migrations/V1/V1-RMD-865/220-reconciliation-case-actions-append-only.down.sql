DROP TRIGGER IF EXISTS trg_case_actions_clear_guard ON reconciliation.case_actions;
DROP TRIGGER IF EXISTS trg_case_actions_guard ON reconciliation.case_actions;
DROP FUNCTION IF EXISTS reconciliation.case_actions_guard();
