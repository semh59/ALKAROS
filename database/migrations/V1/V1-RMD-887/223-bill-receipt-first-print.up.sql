-- The first printed information receipt of a bill is the original; every later one is a copy. The state lives on the bill so
-- it survives a page reload and holds for every terminal. NULL means no original has been printed yet.
ALTER TABLE billing.bills ADD COLUMN IF NOT EXISTS receipt_first_printed_at TIMESTAMPTZ NULL;
ALTER TABLE billing.bills ADD COLUMN IF NOT EXISTS receipt_first_printed_by UUID NULL;
ALTER TABLE billing.bills DROP CONSTRAINT IF EXISTS ck_bills_receipt_first_print_pair;
ALTER TABLE billing.bills ADD CONSTRAINT ck_bills_receipt_first_print_pair
    CHECK ((receipt_first_printed_at IS NULL) = (receipt_first_printed_by IS NULL));
