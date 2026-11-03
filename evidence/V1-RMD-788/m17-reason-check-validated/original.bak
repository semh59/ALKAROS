-- V1-RMD-788: a cash-out above the manager-set threshold is a request that a manager approves; the approval writes
-- the CashOut ledger row. self_approved marks an approval by the requester (allowed only while the shared
-- "manager self approval" setting is on).
CREATE TABLE IF NOT EXISTS cash.cash_movement_requests (
    request_id           UUID          NOT NULL,
    cash_session_id      UUID          NOT NULL,
    amount               NUMERIC(18,2) NOT NULL CHECK (amount > 0),
    reason               TEXT          NOT NULL,
    idempotency_key      TEXT          NOT NULL,
    status               TEXT          NOT NULL CHECK (status IN ('Requested', 'Approved', 'Rejected', 'Cancelled')),
    requested_by         UUID          NOT NULL,
    requested_at         TIMESTAMPTZ   NOT NULL,
    decided_by           UUID          NULL,
    decided_at           TIMESTAMPTZ   NULL,
    decision_note        TEXT          NULL,
    self_approved        BOOLEAN       NOT NULL DEFAULT FALSE,
    cash_transaction_id  UUID          NULL,
    PRIMARY KEY (request_id),
    CONSTRAINT fk_cash_movement_requests_session FOREIGN KEY (cash_session_id)
        REFERENCES cash.cash_sessions (cash_session_id),
    CONSTRAINT fk_cash_movement_requests_requested_by FOREIGN KEY (requested_by)
        REFERENCES identity.users (user_id),
    CONSTRAINT fk_cash_movement_requests_decided_by FOREIGN KEY (decided_by)
        REFERENCES identity.users (user_id),
    CONSTRAINT fk_cash_movement_requests_transaction FOREIGN KEY (cash_transaction_id)
        REFERENCES cash.cash_transactions (cash_transaction_id),
    CONSTRAINT uq_cash_movement_requests_session_key UNIQUE (cash_session_id, idempotency_key),
    CONSTRAINT ck_cash_movement_requests_reason CHECK (btrim(reason) <> '' AND char_length(reason) <= 500),
    CONSTRAINT ck_cash_movement_requests_note CHECK (decision_note IS NULL OR (btrim(decision_note) <> '' AND char_length(decision_note) <= 500)),
    CONSTRAINT ck_cash_movement_requests_decision CHECK (
        (status = 'Requested' AND decided_by IS NULL AND decided_at IS NULL)
        OR (status <> 'Requested' AND decided_by IS NOT NULL AND decided_at IS NOT NULL)),
    CONSTRAINT ck_cash_movement_requests_ledger_row CHECK ((status = 'Approved') = (cash_transaction_id IS NOT NULL)),
    CONSTRAINT ck_cash_movement_requests_self_approved CHECK (NOT self_approved OR status = 'Approved')
);

-- One ledger row belongs to at most one request.
CREATE UNIQUE INDEX IF NOT EXISTS ux_cash_movement_requests_transaction
    ON cash.cash_movement_requests (cash_transaction_id) WHERE cash_transaction_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_cash_movement_requests_open
    ON cash.cash_movement_requests (cash_session_id) WHERE status = 'Requested';

-- NOT VALID: rows written before this migration may carry no reason; every new row must.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cash_transactions_movement_reason') THEN
        ALTER TABLE cash.cash_transactions
            ADD CONSTRAINT ck_cash_transactions_movement_reason CHECK (
                type NOT IN ('CashIn', 'CashOut') OR (btrim(COALESCE(notes, '')) <> '' AND char_length(notes) <= 500)
            ) NOT VALID;
    END IF;
END
$$;
