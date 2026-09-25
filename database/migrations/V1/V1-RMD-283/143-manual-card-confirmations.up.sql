-- V1-RMD-283: a manager claims "the card WAS charged" for a card payment the system could not confirm
-- (no real terminal exists), and a DIFFERENT authorized person approves it. Only the approval creates a
-- money record. The slip (receipt) number is unique so one receipt can never confirm two payments, which
-- is also what a later bank-statement match runs on.
CREATE TABLE IF NOT EXISTS payments.manual_card_confirmations (
    confirmation_id  UUID          NOT NULL,
    payment_id       UUID          NOT NULL,
    bill_id          UUID          NOT NULL,
    slip_number      TEXT          NOT NULL,
    amount           NUMERIC(18,2) NOT NULL CHECK (amount > 0),
    status           TEXT          NOT NULL CHECK (status IN ('Pending', 'Approved', 'Rejected')),
    requested_by     UUID          NOT NULL,
    requested_at     TIMESTAMPTZ   NOT NULL,
    request_note     TEXT          NULL,
    decided_by       UUID          NULL,
    decided_at       TIMESTAMPTZ   NULL,
    decision_note    TEXT          NULL,
    row_version      BIGINT        NOT NULL DEFAULT 1,
    PRIMARY KEY (confirmation_id),
    CONSTRAINT fk_manual_card_confirmations_payment FOREIGN KEY (payment_id) REFERENCES payments.payments (payment_id),
    CONSTRAINT ck_manual_card_decided_consistency CHECK ((status = 'Pending') = (decided_by IS NULL AND decided_at IS NULL)),
    -- Four-eyes: the person who claims the charge can never be the one who approves it (they may withdraw it).
    CONSTRAINT ck_manual_card_four_eyes CHECK (status <> 'Approved' OR decided_by <> requested_by)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_manual_card_slip_number
    ON payments.manual_card_confirmations (upper(slip_number)) WHERE status <> 'Rejected';
CREATE UNIQUE INDEX IF NOT EXISTS ux_manual_card_one_pending_per_payment
    ON payments.manual_card_confirmations (payment_id) WHERE status = 'Pending';
CREATE INDEX IF NOT EXISTS ix_manual_card_confirmations_bill ON payments.manual_card_confirmations (bill_id);
CREATE INDEX IF NOT EXISTS ix_manual_card_confirmations_requested_at ON payments.manual_card_confirmations (requested_at DESC);
