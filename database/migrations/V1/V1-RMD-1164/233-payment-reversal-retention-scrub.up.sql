-- The payment reversal ledger stays append-only except for one change: the KVKK retention job may replace the free-text reason
-- with the anonymized marker. Every other column must be identical, so an amount, a type or a date can never be edited this way.
CREATE OR REPLACE FUNCTION payments.payment_reversals_guard() RETURNS trigger AS $$
DECLARE
    approved NUMERIC(18,2);
    already  NUMERIC(18,2);
BEGIN
    IF TG_OP = 'UPDATE' AND NEW.reason = '[anonymized]' AND (to_jsonb(NEW) - 'reason') = (to_jsonb(OLD) - 'reason') THEN
        RETURN NEW;
    END IF;
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

-- A kitchen slip prints an item note (after "NOT: ") and a reprint reason (after "NEDEN: ") verbatim, and such a text can span several
-- lines. The retention job replaces each of them inside the printed copy as a whole (callers pass them longest first, so a short one
-- cannot cut a longer one that begins with it).
CREATE OR REPLACE FUNCTION privacy.scrub_slip_notes(slip TEXT, notes TEXT[], marker TEXT) RETURNS TEXT
LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE
    note TEXT;
BEGIN
    FOREACH note IN ARRAY COALESCE(notes, ARRAY[]::TEXT[]) LOOP
        IF note <> '' AND note <> marker THEN
            slip := replace(replace(slip, 'NOT: ' || note, 'NOT: ' || marker), 'NEDEN: ' || note, 'NEDEN: ' || marker);
        END IF;
    END LOOP;
    RETURN slip;
END;
$$;
