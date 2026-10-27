-- V1-RMD-732: bill amounts carried no CHECK at all and bills.cancelled_by (added by 180) no foreign key, unlike
-- every other "who did this" column. Both are NOT VALID: they bind new writes without failing on rows written before.
-- ix_bill_items_bill duplicates the leading column of uq_bill_items_bill_item (bill_id, bill_item_id).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_bills_nonnegative_amounts') THEN
        ALTER TABLE billing.bills
            ADD CONSTRAINT ck_bills_nonnegative_amounts
            CHECK (
                subtotal >= 0 AND discount_total >= 0 AND tax_total >= 0 AND payable_amount >= 0
                AND allocated_amount >= 0 AND paid_amount >= 0 AND change_amount >= 0
            ) NOT VALID;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bills_cancelled_by') THEN
        ALTER TABLE billing.bills
            ADD CONSTRAINT fk_bills_cancelled_by
            FOREIGN KEY (cancelled_by) REFERENCES identity.users (user_id) NOT VALID;
    END IF;
END $$;

DROP INDEX IF EXISTS billing.ix_bill_items_bill;
