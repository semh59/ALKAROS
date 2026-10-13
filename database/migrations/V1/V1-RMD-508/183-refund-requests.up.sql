-- A refund of one approved payment, asked for by a cashier and decided by someone else.
-- Cash is paid back by the till once approved; any other method waits for the person to record the manual result.
CREATE TABLE IF NOT EXISTS payments.refund_requests (
    refund_request_id  UUID          NOT NULL,
    bill_id            UUID          NOT NULL,
    payment_id         UUID          NOT NULL,
    amount             NUMERIC(18,2) NOT NULL,
    method             TEXT          NOT NULL,
    reason             TEXT          NOT NULL,
    status             TEXT          NOT NULL,
    requested_by       UUID          NOT NULL,
    requested_at       TIMESTAMPTZ   NOT NULL,
    decided_by         UUID          NULL,
    decided_at         TIMESTAMPTZ   NULL,
    decision_note      TEXT          NULL,
    slip_number        TEXT          NULL,
    completed_by       UUID          NULL,
    completed_at       TIMESTAMPTZ   NULL,
    row_version        BIGINT        NOT NULL DEFAULT 1,
    PRIMARY KEY (refund_request_id),
    CONSTRAINT fk_refund_requests_bill FOREIGN KEY (bill_id) REFERENCES billing.bills (bill_id),
    CONSTRAINT fk_refund_requests_payment FOREIGN KEY (payment_id) REFERENCES payments.payments (payment_id),
    CONSTRAINT ck_refund_requests_amount_positive CHECK (amount > 0),
    CONSTRAINT ck_refund_requests_method CHECK (method IN ('Cash', 'Manual')),
    CONSTRAINT ck_refund_requests_status CHECK (status IN ('Requested', 'AwaitingManualRefund', 'Completed', 'Rejected')),
    CONSTRAINT ck_refund_requests_reason_present CHECK (length(btrim(reason)) > 0),
    CONSTRAINT ck_refund_requests_decider_not_requester CHECK (decided_by IS NULL OR decided_by <> requested_by)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_refund_requests_open_per_payment
    ON payments.refund_requests (payment_id)
    WHERE status IN ('Requested', 'AwaitingManualRefund');
CREATE INDEX IF NOT EXISTS ix_refund_requests_bill ON payments.refund_requests (bill_id);
CREATE INDEX IF NOT EXISTS ix_refund_requests_status ON payments.refund_requests (status);
