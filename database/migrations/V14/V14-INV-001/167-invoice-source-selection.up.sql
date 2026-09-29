-- V14-INV-001: periodic invoice source selection. An operator closes an invoice
-- period (Europe/Istanbul dates, end exclusive); for a closed period each
-- customer's not-yet-invoiced customer_account.account_transactions rows are
-- locked into one source set. Nothing here writes the account ledger, so the
-- selection never changes a balance (V0-DOM-007 invariant 1: a transaction is
-- invoiced in exactly one period).

CREATE SCHEMA IF NOT EXISTS invoicing;

CREATE TABLE IF NOT EXISTS invoicing.invoice_periods (
    period_id     UUID        NOT NULL PRIMARY KEY,
    period_start  DATE        NOT NULL,
    period_end    DATE        NOT NULL,
    closed_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    closed_by     UUID        NOT NULL,
    CONSTRAINT ck_invoice_periods_range CHECK (period_start < period_end),
    CONSTRAINT ux_invoice_periods_range UNIQUE (period_start, period_end)
);

CREATE TABLE IF NOT EXISTS invoicing.invoice_source_sets (
    source_set_id   UUID           NOT NULL PRIMARY KEY,
    period_id       UUID           NOT NULL REFERENCES invoicing.invoice_periods (period_id),
    customer_id     UUID           NOT NULL,
    status          TEXT           NOT NULL DEFAULT 'Selected'
        CHECK (status IN ('Selected', 'Cancelled')),
    debit_total     NUMERIC(12, 2) NOT NULL CHECK (debit_total >= 0),
    credit_total    NUMERIC(12, 2) NOT NULL CHECK (credit_total >= 0),
    line_count      INTEGER        NOT NULL CHECK (line_count > 0),
    selected_at     TIMESTAMPTZ    NOT NULL DEFAULT now(),
    cancelled_at    TIMESTAMPTZ    NULL,
    cancelled_by    UUID           NULL,
    cancel_reason   TEXT           NULL,
    CONSTRAINT ck_invoice_source_sets_cancellation CHECK (
        (status = 'Selected' AND cancelled_at IS NULL AND cancelled_by IS NULL AND cancel_reason IS NULL)
        OR (status = 'Cancelled' AND cancelled_at IS NOT NULL AND cancelled_by IS NOT NULL AND cancel_reason IS NOT NULL))
);

-- One live source set per customer and period: a rerun returns it instead of
-- selecting again.
CREATE UNIQUE INDEX IF NOT EXISTS ux_invoice_source_sets_live
    ON invoicing.invoice_source_sets (period_id, customer_id) WHERE status = 'Selected';

CREATE TABLE IF NOT EXISTS invoicing.invoice_source_lines (
    source_set_id     UUID           NOT NULL REFERENCES invoicing.invoice_source_sets (source_set_id),
    transaction_id    UUID           NOT NULL,
    transaction_type  TEXT           NOT NULL,
    direction         TEXT           NOT NULL CHECK (direction IN ('Debit', 'Credit')),
    amount            NUMERIC(12, 2) NOT NULL,
    occurred_at       TIMESTAMPTZ    NOT NULL,
    cancelled         BOOLEAN        NOT NULL DEFAULT false,
    PRIMARY KEY (source_set_id, transaction_id)
);

-- The invariant itself: a ledger transaction belongs to at most one source set
-- that is not cancelled.
CREATE UNIQUE INDEX IF NOT EXISTS ux_invoice_source_lines_live_transaction
    ON invoicing.invoice_source_lines (transaction_id) WHERE NOT cancelled;
