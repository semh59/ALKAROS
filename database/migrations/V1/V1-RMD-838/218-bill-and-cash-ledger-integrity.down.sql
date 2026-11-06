DROP TRIGGER IF EXISTS trg_cash_transactions_clear_guard ON cash.cash_transactions;
DROP TRIGGER IF EXISTS trg_cash_transactions_guard ON cash.cash_transactions;
DROP FUNCTION IF EXISTS cash.cash_transactions_guard();

ALTER TABLE billing.bill_adjustments DROP CONSTRAINT IF EXISTS fk_bill_adjustments_item;
ALTER TABLE billing.bill_adjustments
    ADD CONSTRAINT fk_bill_adjustments_item
    FOREIGN KEY (bill_id, bill_item_id) REFERENCES billing.bill_items (bill_id, bill_item_id) ON DELETE CASCADE;

ALTER TABLE billing.bill_allocations DROP CONSTRAINT IF EXISTS fk_bill_allocations_item;
ALTER TABLE billing.bill_allocations
    ADD CONSTRAINT fk_bill_allocations_item
    FOREIGN KEY (bill_id, bill_item_id) REFERENCES billing.bill_items (bill_id, bill_item_id) ON DELETE CASCADE;
