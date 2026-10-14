-- A shift refund budget, given by a manager like an opening float: refunds the cashier completes within it need no second person.
ALTER TABLE cash.cash_sessions
    ADD COLUMN IF NOT EXISTS refund_allowance      NUMERIC(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS refund_allowance_used NUMERIC(18,2) NOT NULL DEFAULT 0;
ALTER TABLE cash.cash_sessions DROP CONSTRAINT IF EXISTS ck_cash_sessions_refund_allowance;
ALTER TABLE cash.cash_sessions ADD CONSTRAINT ck_cash_sessions_refund_allowance
    CHECK (refund_allowance >= 0 AND refund_allowance_used >= 0 AND refund_allowance_used <= refund_allowance);

ALTER TABLE payments.refund_requests ADD COLUMN IF NOT EXISTS approval_mode TEXT NULL;
ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_approval_mode;
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_approval_mode
    CHECK (approval_mode IS NULL OR approval_mode IN ('Manager', 'Allowance'));
ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_decider_not_requester;
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_decider_not_requester
    CHECK (decided_by IS NULL OR decided_by <> requested_by OR approval_mode = 'Allowance');
