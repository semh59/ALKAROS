DROP INDEX IF EXISTS payments.uq_refund_requests_idempotency;
ALTER TABLE payments.refund_requests DROP COLUMN IF EXISTS idempotency_key;
