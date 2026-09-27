DROP INDEX IF EXISTS payments.ix_payments_status;

ALTER TABLE payments.payments
    DROP CONSTRAINT IF EXISTS ck_payments_change_amount_reconciles;

ALTER TABLE payments.payments
    DROP CONSTRAINT IF EXISTS ck_payments_approved_amount_not_exceeding_tendered;

ALTER TABLE payments.payments
    DROP CONSTRAINT IF EXISTS ck_payments_status_approved_amount_pairing;

ALTER TABLE payments.payments
    DROP CONSTRAINT IF EXISTS ck_payments_status_tendered_amount_pairing;
