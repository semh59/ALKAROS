DROP INDEX IF EXISTS cash.ux_cash_transactions_session_idempotency_key;
ALTER TABLE cash.cash_transactions DROP COLUMN IF EXISTS idempotency_key;
