-- V1-RMD-242: applies this codebase's own established norm
-- ("Cross-module FKs to identity.users are this codebase's consistent
-- norm, not an exception this schema was carved out of" — V1-RMD-191) to
-- the newer V13 Payments/Cash schema, which never got it. RESTRICT
-- (default) for NOT NULL "who did this" columns (same rule V1-RMD-191
-- applied to bill_adjustments.authorized_by); ON DELETE SET NULL for
-- nullable "who closed/approved/recorded/requested" columns (same rule
-- V1-RMD-191 applied to created_by columns) — none of these tables carry
-- an immutability trigger, so SET NULL on the nullable ones is safe.

ALTER TABLE payments.payment_status_history
    ADD CONSTRAINT fk_payment_status_history_changed_by
        FOREIGN KEY (changed_by) REFERENCES identity.users (user_id) ON DELETE SET NULL;

ALTER TABLE cash.cash_sessions
    ADD CONSTRAINT fk_cash_sessions_cashier_user
        FOREIGN KEY (cashier_user_id) REFERENCES identity.users (user_id),
    ADD CONSTRAINT fk_cash_sessions_closed_by
        FOREIGN KEY (closed_by) REFERENCES identity.users (user_id) ON DELETE SET NULL,
    ADD CONSTRAINT fk_cash_sessions_reconciled_by
        FOREIGN KEY (reconciled_by) REFERENCES identity.users (user_id) ON DELETE SET NULL;

ALTER TABLE cash.cash_counts
    ADD CONSTRAINT fk_cash_counts_counted_by
        FOREIGN KEY (counted_by) REFERENCES identity.users (user_id);

ALTER TABLE cash.cash_transactions
    ADD CONSTRAINT fk_cash_transactions_recorded_by
        FOREIGN KEY (recorded_by) REFERENCES identity.users (user_id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_cash_transactions_recorded_by ON cash.cash_transactions (recorded_by);

ALTER TABLE payments.refund_intents
    ADD CONSTRAINT fk_refund_intents_requested_by
        FOREIGN KEY (requested_by) REFERENCES identity.users (user_id) ON DELETE SET NULL;
