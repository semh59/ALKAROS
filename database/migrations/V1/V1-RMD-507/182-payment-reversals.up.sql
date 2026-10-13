-- Append-only ledger of money paid back for an approved payment (docs/domain/refund-ledger.md).
-- Payments and allocations are never edited; a refund is one row here plus the payment's status moving on.
CREATE TABLE IF NOT EXISTS payments.payment_reversals (
    reversal_id      UUID          NOT NULL,
    payment_id       UUID          NOT NULL,
    reversal_type    TEXT          NOT NULL,
    amount           NUMERIC(18,2) NOT NULL,
    reason           TEXT          NOT NULL,
    refunded_by      UUID          NOT NULL,
    idempotency_key  TEXT          NOT NULL,
    occurred_at      TIMESTAMPTZ   NOT NULL,
    created_at       TIMESTAMPTZ   NOT NULL DEFAULT now(),
    PRIMARY KEY (reversal_id),
    CONSTRAINT fk_payment_reversals_payment FOREIGN KEY (payment_id) REFERENCES payments.payments (payment_id),
    CONSTRAINT ck_payment_reversals_type CHECK (reversal_type IN ('FullRefund', 'PartialRefund')),
    CONSTRAINT ck_payment_reversals_amount_positive CHECK (amount > 0),
    CONSTRAINT ck_payment_reversals_reason_present CHECK (length(btrim(reason)) > 0),
    CONSTRAINT uq_payment_reversals_idempotency_key UNIQUE (idempotency_key)
);

CREATE INDEX IF NOT EXISTS ix_payment_reversals_payment ON payments.payment_reversals (payment_id);
CREATE INDEX IF NOT EXISTS ix_payment_reversals_occurred_at ON payments.payment_reversals (occurred_at);

CREATE OR REPLACE FUNCTION payments.payment_reversals_guard() RETURNS trigger AS $$
DECLARE
    approved NUMERIC(18,2);
    already  NUMERIC(18,2);
BEGIN
    IF TG_OP <> 'INSERT' THEN
        RAISE EXCEPTION 'payment_reversals is append-only' USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    SELECT approved_amount INTO approved FROM payments.payments WHERE payment_id = NEW.payment_id FOR UPDATE;
    SELECT COALESCE(SUM(amount), 0) INTO already FROM payments.payment_reversals WHERE payment_id = NEW.payment_id;
    IF approved IS NULL OR already + NEW.amount > approved THEN
        RAISE EXCEPTION 'refund exceeds the approved amount of payment %', NEW.payment_id USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_payment_reversals_guard ON payments.payment_reversals;
CREATE TRIGGER trg_payment_reversals_guard
    BEFORE INSERT OR UPDATE OR DELETE ON payments.payment_reversals
    FOR EACH ROW EXECUTE FUNCTION payments.payment_reversals_guard();

-- The approved amount stays on the row once the payment is refunded, fully or in part.
ALTER TABLE payments.payments DROP CONSTRAINT IF EXISTS ck_payments_status_approved_amount_pairing;
ALTER TABLE payments.payments
    ADD CONSTRAINT ck_payments_status_approved_amount_pairing
        CHECK ((status IN ('Approved', 'Refunded', 'PartiallyRefunded')) = (approved_amount IS NOT NULL));
