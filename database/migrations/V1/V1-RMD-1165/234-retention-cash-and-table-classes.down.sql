-- Removes the policy version that introduced the two classes, puts the seven-class checks back and restores the strict
-- append-only cash ledger guard. It refuses (foreign key or check violation) once a run, hold or work item already used a
-- new class: that audit is never rewritten. Notes already replaced by the marker stay so.
ALTER TABLE privacy.retention_rules DISABLE TRIGGER tr_retention_rules_immutable;
ALTER TABLE privacy.retention_policies DISABLE TRIGGER tr_retention_policies_immutable;
DELETE FROM privacy.retention_rules
WHERE policy_version IN (SELECT policy_version FROM privacy.retention_rules WHERE data_class = 'CashFreeText');
DELETE FROM privacy.retention_policies p
WHERE NOT EXISTS (SELECT 1 FROM privacy.retention_rules r WHERE r.policy_version = p.policy_version);
ALTER TABLE privacy.retention_policies ENABLE TRIGGER tr_retention_policies_immutable;
ALTER TABLE privacy.retention_rules ENABLE TRIGGER tr_retention_rules_immutable;

ALTER TABLE privacy.retention_rules DROP CONSTRAINT IF EXISTS retention_rules_data_class_check;
ALTER TABLE privacy.retention_rules ADD CONSTRAINT retention_rules_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'InvoicedCustomerProfile', 'Supplier', 'BillFreeText'));
ALTER TABLE privacy.legal_holds DROP CONSTRAINT IF EXISTS legal_holds_data_class_check;
ALTER TABLE privacy.legal_holds ADD CONSTRAINT legal_holds_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'InvoicedCustomerProfile', 'Supplier', 'BillFreeText'));
ALTER TABLE privacy.retention_work_items DROP CONSTRAINT IF EXISTS retention_work_items_data_class_check;
ALTER TABLE privacy.retention_work_items ADD CONSTRAINT retention_work_items_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'InvoicedCustomerProfile', 'Supplier', 'BillFreeText'));
ALTER TABLE privacy.anonymization_jobs DROP CONSTRAINT IF EXISTS anonymization_jobs_data_class_check;
ALTER TABLE privacy.anonymization_jobs ADD CONSTRAINT anonymization_jobs_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'InvoicedCustomerProfile', 'Supplier', 'BillFreeText'));

CREATE OR REPLACE FUNCTION privacy.assert_policy_complete() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF (SELECT count(*) FROM privacy.retention_rules WHERE policy_version = NEW.policy_version) <> 7 THEN
        RAISE EXCEPTION 'retention policy % must define a window for every data class', NEW.policy_version
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NULL;
END;
$$;

CREATE OR REPLACE FUNCTION cash.cash_transactions_guard() RETURNS trigger AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND OLD.recorded_by IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM identity.users WHERE user_id = OLD.recorded_by) THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'cash_transactions is append-only' USING ERRCODE = 'integrity_constraint_violation';
END;
$$ LANGUAGE plpgsql;
