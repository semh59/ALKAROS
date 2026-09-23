-- V1-RMD-258: Faz 2 independent audit remediation for V13-PAY-004's
-- payments.card_settlement_attempts (migration 140). Two real, narrow,
-- schema-level gaps found and confirmed by tracing the actual code:
--
-- 1. No bill_id column existed at all — a replay of a reused idempotency
--    key against a DIFFERENT bill than the one it was first recorded under
--    (a key-generation bug, or a stale/replayed client context) would have
--    returned that other bill's real result as if it were a valid replay
--    for this one. bill_id is backfilled from the attempt's own
--    payments.payments.bill_id (the FK already in place on migration 140
--    guarantees every existing row has a resolvable payment_id).
-- 2. allocation_id had no UNIQUE constraint — nothing in the schema itself
--    stopped two different attempt rows from referencing the SAME
--    allocation. Unreachable via CardSettlementOrchestrator's own logic
--    today (it always creates a fresh allocation per attempt in the same
--    transaction as the attempt row), but a real, cheap-to-close
--    defense-in-depth gap the database itself should also guard, not just
--    call-site discipline.
--
-- Additive only: no existing column, constraint, or row is altered or
-- dropped beyond the backfill itself.
ALTER TABLE payments.card_settlement_attempts
    ADD COLUMN IF NOT EXISTS bill_id UUID NULL;

UPDATE payments.card_settlement_attempts AS attempts
SET bill_id = payments.bill_id
FROM payments.payments AS payments
WHERE attempts.payment_id = payments.payment_id
  AND attempts.bill_id IS NULL;

ALTER TABLE payments.card_settlement_attempts
    ALTER COLUMN bill_id SET NOT NULL;

ALTER TABLE payments.card_settlement_attempts
    ADD CONSTRAINT fk_card_settlement_attempts_bill
        FOREIGN KEY (bill_id) REFERENCES billing.bills (bill_id);

CREATE INDEX IF NOT EXISTS ix_card_settlement_attempts_bill
    ON payments.card_settlement_attempts (bill_id);

CREATE UNIQUE INDEX IF NOT EXISTS uq_card_settlement_attempts_allocation
    ON payments.card_settlement_attempts (allocation_id)
    WHERE allocation_id IS NOT NULL;
