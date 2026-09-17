CREATE SCHEMA IF NOT EXISTS payments;

CREATE TABLE IF NOT EXISTS payments.payments (
    payment_id        UUID          NOT NULL,
    bill_id           UUID          NOT NULL,
    status            TEXT          NOT NULL CHECK (status IN ('Initiated', 'Pending', 'Approved', 'Declined', 'Cancelled', 'Unknown', 'ReconciliationRequired', 'Refunded', 'PartiallyRefunded')),
    currency_code     CHAR(3)       NOT NULL DEFAULT 'TRY',
    requested_amount  NUMERIC(18,2) NOT NULL,
    tendered_amount   NUMERIC(18,2) NULL,
    approved_amount   NUMERIC(18,2) NULL,
    change_amount     NUMERIC(18,2) NOT NULL DEFAULT 0,
    initiated_at      TIMESTAMPTZ   NOT NULL,
    tendered_at       TIMESTAMPTZ   NULL,
    approved_at       TIMESTAMPTZ   NULL,
    declined_at       TIMESTAMPTZ   NULL,
    cancelled_at      TIMESTAMPTZ   NULL,
    created_at        TIMESTAMPTZ   NOT NULL,
    updated_at        TIMESTAMPTZ   NOT NULL,
    row_version       BIGINT        NOT NULL DEFAULT 1,
    PRIMARY KEY (payment_id),
    CONSTRAINT fk_payments_bill FOREIGN KEY (bill_id) REFERENCES billing.bills (bill_id),
    CONSTRAINT ck_payments_requested_amount_positive CHECK (requested_amount > 0),
    CONSTRAINT ck_payments_tendered_amount_positive CHECK (tendered_amount IS NULL OR tendered_amount > 0),
    CONSTRAINT ck_payments_approved_amount_positive CHECK (approved_amount IS NULL OR approved_amount > 0),
    CONSTRAINT ck_payments_change_amount_nonnegative CHECK (change_amount >= 0)
);

CREATE INDEX IF NOT EXISTS ix_payments_bill ON payments.payments (bill_id);

CREATE TABLE IF NOT EXISTS payments.payment_status_history (
    payment_status_history_id  UUID          NOT NULL,
    payment_id                 UUID          NOT NULL,
    old_status                 TEXT          NOT NULL,
    new_status                 TEXT          NOT NULL,
    reason                     TEXT          NULL,
    changed_by                 UUID          NULL,
    changed_at                 TIMESTAMPTZ   NOT NULL,
    PRIMARY KEY (payment_status_history_id),
    CONSTRAINT fk_payment_status_history_payment FOREIGN KEY (payment_id) REFERENCES payments.payments (payment_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_payment_status_history_payment ON payments.payment_status_history (payment_id);
