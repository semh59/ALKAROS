-- V12-RMD-004: a failing webhook event waits before its next attempt (exponential, bounded) instead of
-- using up every attempt in seconds; and one provider order can never become two local orders even if a
-- future path forgets the per-order lock.
ALTER TABLE online_ordering.yemeksepeti_webhook_inbox
    ADD COLUMN IF NOT EXISTS next_attempt_at TIMESTAMPTZ NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_orders_online_source_external_id
    ON orders.orders (source_external_id)
    WHERE source = 'Online';
