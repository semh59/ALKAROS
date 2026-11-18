-- Free text on bills (item and adjustment notes, cancellation reason, closed refund requests) becomes its own retention
-- class, keyed on the bill. Its window is the order window: a bill is anonymized when its orders would be. Policy versions
-- are immutable, so a new version carries the windows of the latest one and adds only the new class.
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

WITH latest AS (SELECT max(policy_version) AS version FROM privacy.retention_policies),
     published AS (
         INSERT INTO privacy.retention_policies (policy_version, published_by, note)
         SELECT version + 1, NULL, 'Bill free texts: anonymized with the order window'
         FROM latest
         WHERE NOT EXISTS (SELECT 1 FROM privacy.retention_rules WHERE data_class = 'BillFreeText')
         RETURNING policy_version)
INSERT INTO privacy.retention_rules (policy_version, data_class, retention_years)
SELECT published.policy_version, r.data_class, r.retention_years
FROM published, latest, privacy.retention_rules r
WHERE r.policy_version = latest.version
UNION ALL
SELECT published.policy_version, 'BillFreeText', r.retention_years
FROM published, latest, privacy.retention_rules r
WHERE r.policy_version = latest.version AND r.data_class = 'OrderNotes';
