-- Removes the policy version that introduced the class and puts the five-class checks back. It refuses (foreign key or
-- check violation) once a run, hold or work item already used the new class: that audit is never rewritten.
ALTER TABLE privacy.retention_rules DISABLE TRIGGER tr_retention_rules_immutable;
ALTER TABLE privacy.retention_policies DISABLE TRIGGER tr_retention_policies_immutable;
DELETE FROM privacy.retention_rules
WHERE policy_version IN (SELECT policy_version FROM privacy.retention_rules WHERE data_class = 'InvoicedCustomerProfile');
DELETE FROM privacy.retention_policies p
WHERE NOT EXISTS (SELECT 1 FROM privacy.retention_rules r WHERE r.policy_version = p.policy_version);
ALTER TABLE privacy.retention_policies ENABLE TRIGGER tr_retention_policies_immutable;
ALTER TABLE privacy.retention_rules ENABLE TRIGGER tr_retention_rules_immutable;

ALTER TABLE privacy.anonymization_jobs DROP CONSTRAINT IF EXISTS anonymization_jobs_data_class_check;
ALTER TABLE privacy.anonymization_jobs ADD CONSTRAINT anonymization_jobs_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'Supplier'));
ALTER TABLE privacy.retention_work_items DROP CONSTRAINT IF EXISTS retention_work_items_data_class_check;
ALTER TABLE privacy.retention_work_items ADD CONSTRAINT retention_work_items_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'Supplier'));
ALTER TABLE privacy.legal_holds DROP CONSTRAINT IF EXISTS legal_holds_data_class_check;
ALTER TABLE privacy.legal_holds ADD CONSTRAINT legal_holds_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'Supplier'));
ALTER TABLE privacy.retention_rules DROP CONSTRAINT IF EXISTS retention_rules_data_class_check;
ALTER TABLE privacy.retention_rules ADD CONSTRAINT retention_rules_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'Supplier'));

CREATE OR REPLACE FUNCTION privacy.assert_policy_complete() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF (SELECT count(*) FROM privacy.retention_rules WHERE policy_version = NEW.policy_version) <> 5 THEN
        RAISE EXCEPTION 'retention policy % must define a window for every data class', NEW.policy_version
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NULL;
END;
$$;
