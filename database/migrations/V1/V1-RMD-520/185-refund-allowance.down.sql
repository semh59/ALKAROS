ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_decider_not_requester;
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_decider_not_requester
    CHECK (decided_by IS NULL OR decided_by <> requested_by);
ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_approval_mode;
ALTER TABLE payments.refund_requests DROP COLUMN IF EXISTS approval_mode;
ALTER TABLE cash.cash_sessions DROP CONSTRAINT IF EXISTS ck_cash_sessions_refund_allowance;
ALTER TABLE cash.cash_sessions DROP COLUMN IF EXISTS refund_allowance_used, DROP COLUMN IF EXISTS refund_allowance;
