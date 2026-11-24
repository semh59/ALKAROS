-- The payment method is read from these records, so dropping them would turn recorded card payments into transfers.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM payments.external_card_receipts)
       OR EXISTS (SELECT 1 FROM payments.refund_requests WHERE method = 'ExternalCard') THEN
        RAISE EXCEPTION 'External card receipts or refunds exist; rolling back would turn them into transfers.';
    END IF;
END $$;

ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_method;
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_method CHECK (method IN ('Cash', 'Card', 'Eft'));

DROP TABLE IF EXISTS payments.external_card_receipts;
