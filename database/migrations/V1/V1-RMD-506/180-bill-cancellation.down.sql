ALTER TABLE billing.bills DROP COLUMN IF EXISTS cancelled_by;
ALTER TABLE billing.bills DROP COLUMN IF EXISTS cancellation_reason;

-- Restoring plain uniqueness fails when a cancelled and an active bill (or superseded items) share a value;
-- those rows must be cleaned up by hand first.
DROP INDEX IF EXISTS billing.bill_items_order_item_id_key;
ALTER TABLE billing.bill_items ADD CONSTRAINT bill_items_order_item_id_key UNIQUE (order_item_id);
ALTER TABLE billing.bill_items DROP COLUMN IF EXISTS superseded_at;

DROP INDEX IF EXISTS billing.bills_bill_number_key;
ALTER TABLE billing.bills ADD CONSTRAINT bills_bill_number_key UNIQUE (bill_number);
