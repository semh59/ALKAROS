ALTER TABLE billing.bills DROP CONSTRAINT IF EXISTS ck_bills_receipt_first_print_pair;
ALTER TABLE billing.bills DROP COLUMN IF EXISTS receipt_first_printed_by;
ALTER TABLE billing.bills DROP COLUMN IF EXISTS receipt_first_printed_at;
