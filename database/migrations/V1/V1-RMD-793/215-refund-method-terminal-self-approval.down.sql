-- Self approved decisions cannot satisfy the older four-eyes rules, and rewriting them would falsify who decided:
-- the rollback refuses while any exist.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM payments.manual_card_confirmations WHERE self_approved)
       OR EXISTS (SELECT 1 FROM payments.refund_requests WHERE approval_mode = 'Self') THEN
        RAISE EXCEPTION 'Self approved refund or card decisions exist; they cannot be rolled back without losing who decided them.';
    END IF;
END $$;

ALTER TABLE payments.manual_card_confirmations DROP CONSTRAINT IF EXISTS ck_manual_card_self_approved;
ALTER TABLE payments.manual_card_confirmations
    ADD CONSTRAINT ck_manual_card_four_eyes CHECK (status <> 'Approved' OR decided_by <> requested_by);
ALTER TABLE payments.manual_card_confirmations DROP COLUMN IF EXISTS self_approved;

ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_self_mode;
ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_decider_not_requester;
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_decider_not_requester
    CHECK (decided_by IS NULL OR decided_by <> requested_by OR approval_mode = 'Allowance');
ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_approval_mode;
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_approval_mode
    CHECK (approval_mode IS NULL OR approval_mode IN ('Manager', 'Allowance'));

ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_method;
UPDATE payments.refund_requests SET method = 'Manual' WHERE method IN ('Card', 'Eft');
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_method CHECK (method IN ('Cash', 'Manual'));

ALTER TABLE payments.refund_requests DROP COLUMN IF EXISTS terminal_id;
