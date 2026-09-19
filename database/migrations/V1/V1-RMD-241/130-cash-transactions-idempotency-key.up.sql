-- V1-RMD-241: manual cash-in/cash-out (unlike cash-tender) had no
-- idempotency key at all, so a network retry could post the same
-- movement twice. Nullable so every existing production entry
-- (Opening/Sale/CashOut/Refund/CountAdjustment/ClosingDifference) is
-- unaffected; the unique index only applies where a key is actually given.
ALTER TABLE cash.cash_transactions
    ADD COLUMN IF NOT EXISTS idempotency_key TEXT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_cash_transactions_session_idempotency_key
    ON cash.cash_transactions (cash_session_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;
