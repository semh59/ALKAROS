-- Drops the slip helper and puts the append-only guard back as it was: every UPDATE and DELETE is refused. Reasons already replaced by the marker stay so.
DROP FUNCTION IF EXISTS privacy.scrub_slip_notes(TEXT, TEXT[], TEXT);
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
