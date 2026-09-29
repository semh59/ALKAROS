-- V14-ACC-004: a payment a customer makes towards their receivable account,
-- independent of any Bill. Only the method-specific owner (cash receipt,
-- card receipt) moves it out of Requested, and Approved always carries the
-- evidence that proves the money arrived.
CREATE TABLE IF NOT EXISTS customer_account.account_payments (
    account_payment_id  UUID           NOT NULL PRIMARY KEY,
    customer_id         UUID           NOT NULL,
    method              TEXT           NOT NULL CHECK (method IN ('Cash', 'BankCard')),
    amount              NUMERIC(12, 2) NOT NULL CHECK (amount > 0),
    currency_code       CHAR(3)        NOT NULL DEFAULT 'TRY',
    status              TEXT           NOT NULL CHECK (status IN ('Requested', 'Approved', 'Declined', 'Unknown')),
    idempotency_key     TEXT           NOT NULL,
    evidence_type       TEXT           NULL CHECK (evidence_type IN ('CashTransaction', 'CardProviderReference')),
    evidence_reference  TEXT           NULL,
    requested_by        UUID           NULL,
    requested_at        TIMESTAMPTZ    NOT NULL,
    updated_at          TIMESTAMPTZ    NOT NULL,
    row_version         BIGINT         NOT NULL DEFAULT 1,
    CONSTRAINT ux_account_payments_idempotency_key UNIQUE (idempotency_key),
    -- Approved exists exactly with its evidence; the evidence type follows the method.
    CONSTRAINT ck_account_payments_evidence_pair CHECK ((evidence_type IS NULL) = (evidence_reference IS NULL)),
    CONSTRAINT ck_account_payments_approved_evidence CHECK (status <> 'Approved' OR evidence_reference IS NOT NULL),
    CONSTRAINT ck_account_payments_evidence_method CHECK (
        evidence_type IS NULL
        OR (method = 'Cash' AND evidence_type = 'CashTransaction')
        OR (method = 'BankCard' AND evidence_type = 'CardProviderReference'))
);

-- One piece of evidence (a cash transaction, a provider reference) proves one account payment only.
CREATE UNIQUE INDEX IF NOT EXISTS ux_account_payments_evidence
    ON customer_account.account_payments (evidence_type, evidence_reference)
    WHERE evidence_reference IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_account_payments_customer
    ON customer_account.account_payments (customer_id, requested_at);

CREATE TABLE IF NOT EXISTS customer_account.account_payment_status_history (
    account_payment_status_history_id  UUID        NOT NULL PRIMARY KEY,
    account_payment_id                 UUID        NOT NULL
        REFERENCES customer_account.account_payments (account_payment_id),
    old_status                         TEXT        NULL,
    new_status                         TEXT        NOT NULL,
    evidence_reference                 TEXT        NULL,
    reason                             TEXT        NULL,
    changed_by                         UUID        NULL,
    changed_at                         TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_account_payment_status_history_payment
    ON customer_account.account_payment_status_history (account_payment_id, changed_at);

-- The status history is the audit trail of an account payment: append-only.
CREATE OR REPLACE FUNCTION customer_account.prevent_account_payment_history_modification()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'account_payment_status_history is append-only. UPDATE and DELETE operations are strictly forbidden.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_account_payment_status_history_immutable ON customer_account.account_payment_status_history;

CREATE TRIGGER trg_account_payment_status_history_immutable
BEFORE UPDATE OR DELETE ON customer_account.account_payment_status_history
FOR EACH ROW EXECUTE FUNCTION customer_account.prevent_account_payment_history_modification();
