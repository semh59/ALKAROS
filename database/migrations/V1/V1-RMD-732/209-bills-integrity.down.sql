CREATE INDEX IF NOT EXISTS ix_bill_items_bill ON billing.bill_items (bill_id);

ALTER TABLE billing.bills DROP CONSTRAINT IF EXISTS fk_bills_cancelled_by;
ALTER TABLE billing.bills DROP CONSTRAINT IF EXISTS ck_bills_nonnegative_amounts;
