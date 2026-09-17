-- V13-CSH-002: append-only CashTransaction ledger (docs/domain/
-- cash-session-design.md §4-5, PDF:I.38-I.44/II.2.7/II.5.9/III.9.2).
CREATE TABLE IF NOT EXISTS cash.cash_transactions (
    cash_transaction_id  UUID          NOT NULL,
    cash_session_id      UUID          NOT NULL,
    type                 TEXT          NOT NULL CHECK (type IN (
                             'Opening', 'Sale', 'CashIn', 'CashOut', 'Refund',
                             'CountAdjustment', 'ClosingDifference')),
    direction            TEXT          NOT NULL CHECK (direction IN ('In', 'Out')),
    amount               NUMERIC(18,2) NOT NULL CHECK (amount > 0),
    related_payment_id   UUID          NULL,
    notes                TEXT          NULL,
    recorded_by          UUID          NULL,
    occurred_at          TIMESTAMPTZ   NOT NULL,
    PRIMARY KEY (cash_transaction_id),
    CONSTRAINT fk_cash_transactions_session FOREIGN KEY (cash_session_id)
        REFERENCES cash.cash_sessions (cash_session_id),
    CONSTRAINT fk_cash_transactions_payment FOREIGN KEY (related_payment_id)
        REFERENCES payments.payments (payment_id),
    -- Fixed-direction types (application-level rule, defense in depth):
    -- Opening/Sale/CashIn are always In, CashOut/Refund are always Out.
    -- CountAdjustment/ClosingDifference may legitimately go either way.
    CONSTRAINT ck_cash_transactions_direction_matches_type CHECK (
        (type IN ('Opening', 'Sale', 'CashIn') AND direction = 'In')
        OR (type IN ('CashOut', 'Refund') AND direction = 'Out')
        OR type IN ('CountAdjustment', 'ClosingDifference')
    ),
    -- Payment linkage (In scope): a Sale/Refund entry names the Payment it
    -- moves cash for; every other type has none.
    CONSTRAINT ck_cash_transactions_payment_linkage CHECK (
        (type IN ('Sale', 'Refund')) = (related_payment_id IS NOT NULL)
    ),
    -- Explicit correction requirement: a CountAdjustment must always state why.
    CONSTRAINT ck_cash_transactions_count_adjustment_reason CHECK (
        type <> 'CountAdjustment' OR notes IS NOT NULL
    )
);

CREATE INDEX IF NOT EXISTS ix_cash_transactions_session ON cash.cash_transactions (cash_session_id);
CREATE INDEX IF NOT EXISTS ix_cash_transactions_payment ON cash.cash_transactions (related_payment_id);
