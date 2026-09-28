-- V0-DAT-004 (docs/data/projection-ownership.md row "AccountBalance"):
-- source-of-truth AccountTransaction, writer is the transaction-posted
-- event itself, update happens in the SAME transaction as the write. A
-- trigger (not application code) is the only way to guarantee that without
-- requiring every future caller of account_transactions to also remember to
-- call a balance-update API in the same DB transaction.
CREATE TABLE IF NOT EXISTS customer_account.balances (
    customer_id           UUID           NOT NULL PRIMARY KEY,
    current_balance       NUMERIC(12, 2) NOT NULL,
    last_transaction_at   TIMESTAMPTZ    NOT NULL,
    updated_at            TIMESTAMPTZ    NOT NULL
);

-- V14-ACC-002's own snapshot uniqueness rule: one balance snapshot per
-- customer per date.
CREATE TABLE IF NOT EXISTS customer_account.balance_snapshots (
    id              UUID           NOT NULL PRIMARY KEY,
    customer_id     UUID           NOT NULL,
    snapshot_date   DATE           NOT NULL,
    balance         NUMERIC(12, 2) NOT NULL,
    created_at      TIMESTAMPTZ    NOT NULL,
    UNIQUE (customer_id, snapshot_date)
);

CREATE INDEX IF NOT EXISTS ix_balance_snapshots_customer
    ON customer_account.balance_snapshots (customer_id, snapshot_date);

-- Mirrors AccountTransaction.SignedBalanceEffect exactly (ALKAROS.CustomerAccounts.
-- TransactionLedger): Adjustment already carries its own sign in amount; every
-- other type stores a non-negative magnitude, so Debit contributes +amount
-- and Credit contributes -amount.
CREATE OR REPLACE FUNCTION customer_account.apply_transaction_to_balance()
RETURNS TRIGGER AS $$
DECLARE
    signed_effect NUMERIC(12, 2);
BEGIN
    IF NEW.transaction_type = 'Adjustment' THEN
        signed_effect := NEW.amount;
    ELSIF NEW.direction = 'Debit' THEN
        signed_effect := NEW.amount;
    ELSE
        signed_effect := -NEW.amount;
    END IF;

    INSERT INTO customer_account.balances (customer_id, current_balance, last_transaction_at, updated_at)
    VALUES (NEW.customer_id, signed_effect, NEW.occurred_at, now())
    ON CONFLICT (customer_id) DO UPDATE
        SET current_balance = customer_account.balances.current_balance + signed_effect,
            last_transaction_at = GREATEST(customer_account.balances.last_transaction_at, EXCLUDED.last_transaction_at),
            updated_at = now();

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_account_transactions_apply_balance ON customer_account.account_transactions;

CREATE TRIGGER trg_account_transactions_apply_balance
AFTER INSERT ON customer_account.account_transactions
FOR EACH ROW EXECUTE FUNCTION customer_account.apply_transaction_to_balance();
