-- V12-RMD-004: remove the retry wait and the one-order-per-provider-order index.
DROP INDEX IF EXISTS orders.uq_orders_online_source_external_id;
ALTER TABLE online_ordering.yemeksepeti_webhook_inbox DROP COLUMN IF EXISTS next_attempt_at;
