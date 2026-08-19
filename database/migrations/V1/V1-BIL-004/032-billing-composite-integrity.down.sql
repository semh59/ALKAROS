-- 032-billing-composite-integrity.down.sql
-- Revert composite foreign keys back to simple item foreign keys

ALTER TABLE billing.bill_allocations DROP CONSTRAINT IF EXISTS fk_bill_allocations_item;
ALTER TABLE billing.bill_allocations
    ADD CONSTRAINT fk_bill_allocations_item
    FOREIGN KEY (bill_item_id)
    REFERENCES billing.bill_items (bill_item_id)
    ON DELETE CASCADE;

ALTER TABLE billing.bill_adjustments DROP CONSTRAINT IF EXISTS fk_bill_adjustments_item;
ALTER TABLE billing.bill_adjustments
    ADD CONSTRAINT fk_bill_adjustments_item
    FOREIGN KEY (bill_item_id)
    REFERENCES billing.bill_items (bill_item_id)
    ON DELETE CASCADE;

ALTER TABLE billing.bill_items DROP CONSTRAINT IF EXISTS uq_bill_items_bill_item;
