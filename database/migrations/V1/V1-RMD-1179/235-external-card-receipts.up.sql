-- A card payment taken on a bank POS that is not connected to the system is recorded with the slip (receipt)
-- number printed by that POS. The slip number is a bounded code, never free text and never a card number. The same
-- slip cannot be entered twice on one bill; the same number on another bill is allowed because POS batch numbers repeat.
CREATE TABLE IF NOT EXISTS payments.external_card_receipts (
    payment_id   UUID          NOT NULL,
    bill_id      UUID          NOT NULL,
    terminal_id  UUID          NOT NULL,
    slip_number  TEXT          NOT NULL,
    amount       NUMERIC(18,2) NOT NULL,
    recorded_by  UUID          NOT NULL,
    recorded_at  TIMESTAMPTZ   NOT NULL DEFAULT now(),
    PRIMARY KEY (payment_id),
    CONSTRAINT fk_external_card_receipts_payment FOREIGN KEY (payment_id) REFERENCES payments.payments (payment_id),
    CONSTRAINT ck_external_card_receipts_slip CHECK (slip_number ~ '^[A-Z0-9][A-Z0-9/-]{3,31}$'),
    CONSTRAINT ck_external_card_receipts_amount CHECK (amount > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_external_card_receipts_bill_slip
    ON payments.external_card_receipts (bill_id, slip_number);

ALTER TABLE payments.refund_requests DROP CONSTRAINT IF EXISTS ck_refund_requests_method;
ALTER TABLE payments.refund_requests ADD CONSTRAINT ck_refund_requests_method
    CHECK (method IN ('Cash', 'Card', 'Eft', 'ExternalCard'));
