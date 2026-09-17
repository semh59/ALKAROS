-- V1-RMD-230: defense-in-depth for the Payment aggregate's own
-- constructor invariants (Payment.cs), none of which were previously
-- enforced at the database level -- only application code protected them.
-- Additive only: no existing column, table or CHECK constraint from
-- 120-payments is touched.
ALTER TABLE payments.payments
    ADD CONSTRAINT ck_payments_status_tendered_amount_pairing
        CHECK ((status = 'Initiated') = (tendered_amount IS NULL));

ALTER TABLE payments.payments
    ADD CONSTRAINT ck_payments_status_approved_amount_pairing
        CHECK ((status = 'Approved') = (approved_amount IS NOT NULL));

ALTER TABLE payments.payments
    ADD CONSTRAINT ck_payments_approved_amount_not_exceeding_tendered
        CHECK (approved_amount IS NULL OR approved_amount <= tendered_amount);

ALTER TABLE payments.payments
    ADD CONSTRAINT ck_payments_change_amount_reconciles
        CHECK (approved_amount IS NULL OR change_amount = tendered_amount - approved_amount);

CREATE INDEX IF NOT EXISTS ix_payments_status ON payments.payments (status);
