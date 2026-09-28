CREATE SCHEMA IF NOT EXISTS customer_account;

CREATE TABLE IF NOT EXISTS customer_account.account_transactions (
    id                      UUID          NOT NULL PRIMARY KEY,
    customer_id             UUID          NOT NULL,
    transaction_type        TEXT          NOT NULL
        CHECK (transaction_type IN ('Charge', 'Payment', 'Invoice', 'Credit', 'Debit', 'Adjustment', 'Refund')),
    -- V0-DOM-007 (docs/domain/customer-credit-invoice-semantics.md):
    -- "direction is never written by the application; it is derived by the
    -- generated column." Fixed for every type except Adjustment, whose
    -- direction follows the sign of its own amount.
    direction               TEXT          GENERATED ALWAYS AS (
        CASE transaction_type
            WHEN 'Charge'     THEN 'Debit'
            WHEN 'Invoice'    THEN 'Debit'
            WHEN 'Debit'      THEN 'Debit'
            WHEN 'Payment'    THEN 'Credit'
            WHEN 'Credit'     THEN 'Credit'
            WHEN 'Refund'     THEN 'Credit'
            WHEN 'Adjustment' THEN CASE WHEN amount >= 0 THEN 'Debit' ELSE 'Credit' END
        END
    ) STORED,
    amount                  NUMERIC(12, 2) NOT NULL CHECK (amount <> 0),
    source_reference_type   TEXT          NOT NULL,
    source_reference_id     UUID          NOT NULL,
    note                    TEXT          NULL,
    created_by              UUID          NULL,
    occurred_at             TIMESTAMPTZ   NOT NULL,
    -- CORR:C3 / V0-DOM-007 invariant 5: amount is a non-negative magnitude
    -- for every type except Adjustment, which carries its own sign.
    CHECK (transaction_type = 'Adjustment' OR amount > 0),
    -- V0-DOM-007 invariant 5: a negative Adjustment must be explained.
    CHECK (transaction_type <> 'Adjustment' OR amount >= 0 OR (note IS NOT NULL AND created_by IS NOT NULL))
);

-- Idempotency key (Deliverables: retry/idempotency tests): the same
-- real-world source event never creates a second ledger row for the same
-- customer and transaction type.
CREATE UNIQUE INDEX IF NOT EXISTS ux_account_transactions_source
    ON customer_account.account_transactions (customer_id, transaction_type, source_reference_type, source_reference_id);

CREATE INDEX IF NOT EXISTS ix_account_transactions_customer
    ON customer_account.account_transactions (customer_id, occurred_at);

-- Invariant: this is an append-only ledger. In-place edits are rejected,
-- enforced at the database engine level, not only by
-- IAccountTransactionLedger's own missing update/delete methods (mirrors
-- audit.audit_events' own prevent_audit_modification trigger, V1-OPS-001).
CREATE OR REPLACE FUNCTION customer_account.prevent_transaction_modification()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'account_transactions table is append-only. UPDATE and DELETE operations are strictly forbidden.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_account_transactions_immutable ON customer_account.account_transactions;

CREATE TRIGGER trg_account_transactions_immutable
BEFORE UPDATE OR DELETE ON customer_account.account_transactions
FOR EACH ROW EXECUTE FUNCTION customer_account.prevent_transaction_modification();
