ALTER TABLE table_mgmt.table_merges
    ADD COLUMN IF NOT EXISTS original_order_ids UUID[] NOT NULL DEFAULT '{}',
    ADD COLUMN IF NOT EXISTS original_bill_ids  UUID[] NOT NULL DEFAULT '{}';

UPDATE table_mgmt.table_merges
SET original_order_ids = ARRAY[original_order_id]
WHERE original_order_id IS NOT NULL AND original_order_ids = '{}';

UPDATE table_mgmt.table_merges
SET original_bill_ids = ARRAY[original_bill_id]
WHERE original_bill_id IS NOT NULL AND original_bill_ids = '{}';
