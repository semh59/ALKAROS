-- V14-ACC-009: the receipt handed to a customer for a verified account payment, independent of any Bill. Append-only:
-- a receipt is never edited or deleted; one account payment has at most one receipt.
CREATE SEQUENCE IF NOT EXISTS customer_account.account_receipt_number_seq;

CREATE TABLE IF NOT EXISTS customer_account.account_receipts (
    account_receipt_id  UUID           NOT NULL PRIMARY KEY,
    receipt_number      TEXT           NOT NULL,
    customer_id         UUID           NOT NULL,
    account_payment_id  UUID           NOT NULL
        REFERENCES customer_account.account_payments (account_payment_id),
    amount              NUMERIC(12, 2) NOT NULL CHECK (amount > 0),
    currency_code       CHAR(3)        NOT NULL,
    idempotency_key     TEXT           NOT NULL,
    issued_by           UUID           NULL,
    issued_at           TIMESTAMPTZ    NOT NULL,
    CONSTRAINT ux_account_receipts_number UNIQUE (receipt_number),
    CONSTRAINT ux_account_receipts_payment UNIQUE (account_payment_id),
    CONSTRAINT ux_account_receipts_idempotency_key UNIQUE (idempotency_key)
);

CREATE INDEX IF NOT EXISTS ix_account_receipts_customer
    ON customer_account.account_receipts (customer_id, issued_at);

CREATE OR REPLACE FUNCTION customer_account.prevent_account_receipt_modification()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'account_receipts table is append-only. UPDATE and DELETE operations are strictly forbidden.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_account_receipts_immutable ON customer_account.account_receipts;

CREATE TRIGGER trg_account_receipts_immutable
BEFORE UPDATE OR DELETE ON customer_account.account_receipts
FOR EACH ROW EXECUTE FUNCTION customer_account.prevent_account_receipt_modification();
