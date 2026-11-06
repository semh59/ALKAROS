-- Deleting one bill item used to take its audited adjustment and allocation rows with it. NO ACTION refuses that
-- while the bill-level cascade still removes items and their rows together when a whole bill is deleted.
ALTER TABLE billing.bill_allocations DROP CONSTRAINT IF EXISTS fk_bill_allocations_item;
ALTER TABLE billing.bill_allocations
    ADD CONSTRAINT fk_bill_allocations_item
    FOREIGN KEY (bill_id, bill_item_id) REFERENCES billing.bill_items (bill_id, bill_item_id);

ALTER TABLE billing.bill_adjustments DROP CONSTRAINT IF EXISTS fk_bill_adjustments_item;
ALTER TABLE billing.bill_adjustments
    ADD CONSTRAINT fk_bill_adjustments_item
    FOREIGN KEY (bill_id, bill_item_id) REFERENCES billing.bill_items (bill_id, bill_item_id);

-- These two were added NOT VALID so older rows were never checked; a violating row now stops the migration.
ALTER TABLE billing.bills VALIDATE CONSTRAINT ck_bills_nonnegative_amounts;
ALTER TABLE billing.bills VALIDATE CONSTRAINT fk_bills_cancelled_by;

-- The cash ledger is append-only. The only change allowed is the foreign key's own ON DELETE SET NULL on
-- recorded_by, recognised by the recorded user no longer existing when the foreign key action runs.
CREATE OR REPLACE FUNCTION cash.cash_transactions_guard() RETURNS trigger AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND OLD.recorded_by IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM identity.users WHERE user_id = OLD.recorded_by) THEN
        RETURN NEW;
    END IF;
    RAISE EXCEPTION 'cash_transactions is append-only' USING ERRCODE = 'integrity_constraint_violation';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_cash_transactions_guard ON cash.cash_transactions;
CREATE TRIGGER trg_cash_transactions_guard
    BEFORE UPDATE OR DELETE ON cash.cash_transactions
    FOR EACH ROW EXECUTE FUNCTION cash.cash_transactions_guard();

DROP TRIGGER IF EXISTS trg_cash_transactions_clear_guard ON cash.cash_transactions;
CREATE TRIGGER trg_cash_transactions_clear_guard
    BEFORE TRUNCATE ON cash.cash_transactions
    FOR EACH STATEMENT EXECUTE FUNCTION cash.cash_transactions_guard();
