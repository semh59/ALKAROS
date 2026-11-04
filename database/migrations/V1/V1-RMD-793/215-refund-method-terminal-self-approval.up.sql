-- A refund is paid out of the till of the terminal it was asked at, names the way the money goes back
-- (Cash, Card or Eft) and records when the person who asked also decided it.
ALTER TABLE payments.refund_requests ADD COLUMN IF NOT EXISTS terminal_id UUID NULL;

-- Rows made before the column existed: the terminal of the requester's till that was open at the time, when that
-- is unambiguous (a cashier with tills open at two terminals at once leaves the row without a terminal).
UPDATE payments.refund_requests r
SET terminal_id = (
    SELECT (array_agg(DISTINCT s.terminal_id))[1] FROM cash.cash_sessions s
    WHERE s.cashier_user_id = r.requested_by AND s.opened_at <= r.requested_at
      AND (s.closed_at IS NULL OR s.closed_at >= r.requested_at)
    HAVING count(DISTINCT s.terminal_id) = 1)
WHERE r.terminal_id IS NULL;

ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_method;
-- Cash refunds are already 'Cash' and account charges cannot be refunded, so a 'Manual' row was either a card or a
-- transfer: the only payment producers are the cash, account, card and transfer handlers and the manual card
-- confirmation.
UPDATE payments.refund_requests r
SET method = CASE
    WHEN EXISTS (SELECT 1 FROM payments.card_settlement_attempts a WHERE a.payment_id = r.payment_id AND a.outcome = 'Approved')
      OR EXISTS (SELECT 1 FROM payments.manual_card_confirmations m WHERE m.payment_id = r.payment_id AND m.status = 'Approved')
    THEN 'Card' ELSE 'Eft' END
WHERE r.method = 'Manual';
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_method CHECK (method IN ('Cash', 'Card', 'Eft'));

ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_approval_mode;
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_approval_mode
    CHECK (approval_mode IS NULL OR approval_mode IN ('Manager', 'Allowance', 'Self'));
ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_decider_not_requester;
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_decider_not_requester
    CHECK (decided_by IS NULL OR decided_by <> requested_by OR approval_mode IN ('Allowance', 'Self'));
ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_self_mode;
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_self_mode
    CHECK (approval_mode IS DISTINCT FROM 'Self' OR decided_by = requested_by);

ALTER TABLE payments.manual_card_confirmations ADD COLUMN IF NOT EXISTS self_approved BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE payments.manual_card_confirmations DROP CONSTRAINT IF EXISTS ck_manual_card_four_eyes;
ALTER TABLE payments.manual_card_confirmations DROP CONSTRAINT IF EXISTS ck_manual_card_self_approved;
ALTER TABLE payments.manual_card_confirmations ADD CONSTRAINT ck_manual_card_self_approved
    CHECK (COALESCE(status = 'Approved' AND decided_by = requested_by, false) = self_approved);
