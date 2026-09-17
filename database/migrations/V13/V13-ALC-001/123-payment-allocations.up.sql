-- V13-ALC-001: PaymentAllocation persistence (V0-DOM-004, PDF:I.11-I.15/
-- I.26-I.29/II.2.6/II.3.4-II.3.5/II.5.3/III.8, CORR:C4). Additive only:
-- no existing column or constraint on payments.payments or billing.bills
-- is altered or dropped.

-- Composite UNIQUE constraints (both superset the existing single-column
-- primary key, so nothing already stored is restricted further) needed so
-- payment_allocations can express the same-bill and currency-equality
-- invariants as real foreign keys below, rather than a trigger.
ALTER TABLE payments.payments
    ADD CONSTRAINT uq_payments_id_bill UNIQUE (payment_id, bill_id);

ALTER TABLE payments.payments
    ADD CONSTRAINT uq_payments_id_currency UNIQUE (payment_id, currency_code);

ALTER TABLE billing.bills
    ADD CONSTRAINT uq_bills_id_currency UNIQUE (bill_id, currency_code);

CREATE TABLE IF NOT EXISTS payments.payment_allocations (
    payment_allocation_id  UUID          NOT NULL,
    payment_id             UUID          NOT NULL,
    bill_id                UUID          NOT NULL,
    amount                 NUMERIC(18,2) NOT NULL CHECK (amount > 0),
    currency_code          CHAR(3)       NOT NULL,
    idempotency_key        TEXT          NOT NULL,
    allocated_at           TIMESTAMPTZ   NOT NULL,
    PRIMARY KEY (payment_allocation_id),
    CONSTRAINT uq_payment_allocations_idempotency_key UNIQUE (idempotency_key),
    -- Same-bill invariant (V0-DOM-004): a Payment belongs to exactly one
    -- Bill; an allocation can never target a different one.
    CONSTRAINT fk_payment_allocations_payment_bill
        FOREIGN KEY (payment_id, bill_id) REFERENCES payments.payments (payment_id, bill_id),
    -- Currency equality, transitively (allocation.currency = payment.currency
    -- = bill.currency): both composite FKs below must agree with this row's
    -- own currency_code, and the two UNIQUE constraints above already force
    -- a payment/bill to have exactly one currency each.
    CONSTRAINT fk_payment_allocations_payment_currency
        FOREIGN KEY (payment_id, currency_code) REFERENCES payments.payments (payment_id, currency_code),
    CONSTRAINT fk_payment_allocations_bill_currency
        FOREIGN KEY (bill_id, currency_code) REFERENCES billing.bills (bill_id, currency_code)
);

CREATE INDEX IF NOT EXISTS ix_payment_allocations_bill ON payments.payment_allocations (bill_id);
CREATE INDEX IF NOT EXISTS ix_payment_allocations_payment ON payments.payment_allocations (payment_id);
