-- V1-RMD-125: found by an independent audit — InventoryAdjustmentService and
-- WasteRecordingService checked the non-negative on-hand invariant by
-- reading the current balance, then applying the movement in a separate,
-- unlocked round trip; two concurrent decreases could both pass the check
-- against the same stale balance and together drive on_hand_quantity
-- negative. The application fix (guarded, same-transaction atomic upsert,
-- V1-RMD-125) closes the race; this trigger is the unconditional last line
-- of defense so no code path — present or future — can ever persist a
-- negative on-hand balance, even by mistake.
--
-- A plain table CHECK constraint cannot be used here — and neither can a
-- BEFORE trigger. For INSERT ... ON CONFLICT DO UPDATE, Postgres validates
-- CHECK constraints, and fires the BEFORE INSERT row trigger, against the
-- raw candidate row from the VALUES clause *before* it even decides whether
-- a conflict will redirect the statement to the UPDATE branch (a documented
-- Postgres quirk: BEFORE INSERT must run first because it could change the
-- key columns a conflict would be detected on). Verified empirically: with
-- either a CHECK or a BEFORE trigger, a perfectly legal decrease (existing
-- balance minus a smaller delta, e.g. 10 - 3 = 7) was rejected purely
-- because the statement's raw, negative delta (-3) failed validation
-- against the row that never actually gets inserted (the conflict redirects
-- it to UPDATE instead). An AFTER trigger does not have this problem: it
-- only fires once Postgres has committed to one specific branch — AFTER
-- INSERT for a genuinely new row (candidate values, correct since nothing
-- else could have changed them), AFTER UPDATE for a conflict-redirected row
-- (post-SET-clause computed values, e.g. the real 7) — so NEW always
-- reflects the value actually written.
CREATE OR REPLACE FUNCTION inventory.prevent_negative_on_hand_balance()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.on_hand_quantity < 0 THEN
        RAISE EXCEPTION 'inventory.stock_balances.on_hand_quantity cannot go negative (got %)', NEW.on_hand_quantity
            USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_stock_balances_non_negative ON inventory.stock_balances;
CREATE TRIGGER trg_stock_balances_non_negative
AFTER INSERT OR UPDATE ON inventory.stock_balances
FOR EACH ROW EXECUTE FUNCTION inventory.prevent_negative_on_hand_balance();
