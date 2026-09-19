DROP INDEX IF EXISTS cash.ix_cash_transactions_recorded_by;

ALTER TABLE payments.refund_intents
    DROP CONSTRAINT IF EXISTS fk_refund_intents_requested_by;

ALTER TABLE cash.cash_transactions
    DROP CONSTRAINT IF EXISTS fk_cash_transactions_recorded_by;

ALTER TABLE cash.cash_counts
    DROP CONSTRAINT IF EXISTS fk_cash_counts_counted_by;

ALTER TABLE cash.cash_sessions
    DROP CONSTRAINT IF EXISTS fk_cash_sessions_cashier_user,
    DROP CONSTRAINT IF EXISTS fk_cash_sessions_closed_by,
    DROP CONSTRAINT IF EXISTS fk_cash_sessions_reconciled_by;

ALTER TABLE payments.payment_status_history
    DROP CONSTRAINT IF EXISTS fk_payment_status_history_changed_by;
