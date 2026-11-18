-- Cash and table-movement free texts become two retention classes with the order window: CashFreeText is keyed on the cash
-- session, TableMovementReason on the table transfer row. Policy versions are immutable, so version 4 carries the windows
-- of the latest one and adds only the two new classes.
ALTER TABLE privacy.retention_rules DROP CONSTRAINT IF EXISTS retention_rules_data_class_check;
ALTER TABLE privacy.retention_rules ADD CONSTRAINT retention_rules_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'InvoicedCustomerProfile', 'Supplier', 'BillFreeText', 'CashFreeText', 'TableMovementReason'));
ALTER TABLE privacy.legal_holds DROP CONSTRAINT IF EXISTS legal_holds_data_class_check;
ALTER TABLE privacy.legal_holds ADD CONSTRAINT legal_holds_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'InvoicedCustomerProfile', 'Supplier', 'BillFreeText', 'CashFreeText', 'TableMovementReason'));
ALTER TABLE privacy.retention_work_items DROP CONSTRAINT IF EXISTS retention_work_items_data_class_check;
ALTER TABLE privacy.retention_work_items ADD CONSTRAINT retention_work_items_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'InvoicedCustomerProfile', 'Supplier', 'BillFreeText', 'CashFreeText', 'TableMovementReason'));
ALTER TABLE privacy.anonymization_jobs DROP CONSTRAINT IF EXISTS anonymization_jobs_data_class_check;
ALTER TABLE privacy.anonymization_jobs ADD CONSTRAINT anonymization_jobs_data_class_check
    CHECK (data_class IN ('StaffAccount', 'OrderNotes', 'ReservationReason', 'CustomerProfile', 'InvoicedCustomerProfile', 'Supplier', 'BillFreeText', 'CashFreeText', 'TableMovementReason'));

CREATE OR REPLACE FUNCTION privacy.assert_policy_complete() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF (SELECT count(*) FROM privacy.retention_rules WHERE policy_version = NEW.policy_version) <> 9 THEN
        RAISE EXCEPTION 'retention policy % must define a window for every data class', NEW.policy_version
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NULL;
END;
$$;

WITH latest AS (SELECT max(policy_version) AS version FROM privacy.retention_policies),
     published AS (
         INSERT INTO privacy.retention_policies (policy_version, published_by, note)
         SELECT version + 1, NULL, 'Cash and table-movement free texts: anonymized with the order window'
         FROM latest
         WHERE NOT EXISTS (SELECT 1 FROM privacy.retention_rules WHERE data_class = 'CashFreeText')
         RETURNING policy_version)
INSERT INTO privacy.retention_rules (policy_version, data_class, retention_years)
SELECT published.policy_version, r.data_class, r.retention_years
FROM published, latest, privacy.retention_rules r
WHERE r.policy_version = latest.version
UNION ALL
SELECT published.policy_version, c.data_class, r.retention_years
FROM published, latest, privacy.retention_rules r, (VALUES ('CashFreeText'), ('TableMovementReason')) AS c(data_class)
WHERE r.policy_version = latest.version AND r.data_class = 'OrderNotes';

-- The cash ledger stays append-only except for one change: the KVKK retention job may replace an existing free-text note with the
-- anonymized marker. Every other column must be identical, so an amount, a type or a date can never be edited this way.
CREATE OR REPLACE FUNCTION cash.cash_transactions_guard() RETURNS trigger AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF OLD.recorded_by IS NOT NULL AND NOT EXISTS (SELECT 1 FROM identity.users WHERE user_id = OLD.recorded_by) THEN
            RETURN NEW;
        END IF;
        IF NEW.notes = '[anonymized]' AND OLD.notes IS NOT NULL AND (to_jsonb(NEW) - 'notes') = (to_jsonb(OLD) - 'notes') THEN
            RETURN NEW;
        END IF;
    END IF;
    RAISE EXCEPTION 'cash_transactions is append-only' USING ERRCODE = 'integrity_constraint_violation';
END;
$$ LANGUAGE plpgsql;
