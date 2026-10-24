ALTER TABLE table_mgmt.table_merges
    DROP COLUMN IF EXISTS original_bill_ids,
    DROP COLUMN IF EXISTS original_order_ids;
