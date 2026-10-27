ALTER TABLE payments.refund_requests ADD COLUMN IF NOT EXISTS idempotency_key TEXT;

-- A retried request (lost answer) must find its first answer, also after an allowance refund already completed it.
CREATE UNIQUE INDEX IF NOT EXISTS uq_refund_requests_idempotency
    ON payments.refund_requests (bill_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;
