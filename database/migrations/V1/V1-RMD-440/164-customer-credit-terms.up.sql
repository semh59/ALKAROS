-- V1-RMD-440: per-customer credit terms for account charges (PO 2026-09-29).
-- A customer without a row has a credit limit of 0: no charge is posted to
-- their account. payment_term_days NULL means no overdue check.
CREATE TABLE IF NOT EXISTS customer_account.credit_terms (
    customer_id        UUID           NOT NULL PRIMARY KEY,
    credit_limit       NUMERIC(12, 2) NOT NULL CHECK (credit_limit >= 0),
    payment_term_days  INTEGER        NULL CHECK (payment_term_days BETWEEN 1 AND 365),
    updated_at         TIMESTAMPTZ    NOT NULL,
    updated_by         UUID           NOT NULL
);
