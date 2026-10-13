-- A cancelled bill must not block a new bill for the same order.
-- bill_number (BILL-<order number>) and bill_items.order_item_id were plain UNIQUE, so a cancelled bill kept
-- both and "recall the check, edit, send again, open the bill" failed on bills_bill_number_key. Uniqueness now
-- holds only among bills that are not cancelled and among bill items that are not superseded by a cancellation.
ALTER TABLE billing.bills DROP CONSTRAINT IF EXISTS bills_bill_number_key;
CREATE UNIQUE INDEX IF NOT EXISTS bills_bill_number_key
    ON billing.bills (bill_number)
    WHERE status <> 'Cancelled';

ALTER TABLE billing.bill_items ADD COLUMN IF NOT EXISTS superseded_at TIMESTAMPTZ NULL;
ALTER TABLE billing.bill_items DROP CONSTRAINT IF EXISTS bill_items_order_item_id_key;
CREATE UNIQUE INDEX IF NOT EXISTS bill_items_order_item_id_key
    ON billing.bill_items (order_item_id)
    WHERE superseded_at IS NULL;

ALTER TABLE billing.bills ADD COLUMN IF NOT EXISTS cancellation_reason TEXT NULL;
ALTER TABLE billing.bills ADD COLUMN IF NOT EXISTS cancelled_by UUID NULL;
