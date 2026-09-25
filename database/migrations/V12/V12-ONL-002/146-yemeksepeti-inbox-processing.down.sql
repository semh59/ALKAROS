DROP INDEX IF EXISTS online_ordering.ix_yemeksepeti_webhook_inbox_pending;
ALTER TABLE online_ordering.yemeksepeti_webhook_inbox
    DROP CONSTRAINT IF EXISTS ck_yemeksepeti_webhook_inbox_processed,
    DROP COLUMN IF EXISTS last_error,
    DROP COLUMN IF EXISTS processing_attempts,
    DROP COLUMN IF EXISTS outcome_detail,
    DROP COLUMN IF EXISTS order_id,
    DROP COLUMN IF EXISTS processing_outcome,
    DROP COLUMN IF EXISTS processed_at;
