-- Fails while any payment is Refunded or PartiallyRefunded; those rows must be dealt with by hand first.
ALTER TABLE payments.payments DROP CONSTRAINT IF EXISTS ck_payments_status_approved_amount_pairing;
ALTER TABLE payments.payments
    ADD CONSTRAINT ck_payments_status_approved_amount_pairing
        CHECK ((status = 'Approved') = (approved_amount IS NOT NULL));

DROP TRIGGER IF EXISTS trg_payment_reversals_guard ON payments.payment_reversals;
DROP TABLE IF EXISTS payments.payment_reversals;
DROP FUNCTION IF EXISTS payments.payment_reversals_guard();
