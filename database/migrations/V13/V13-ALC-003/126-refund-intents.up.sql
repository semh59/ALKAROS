-- V13-ALC-003: RefundIntent persistence (V0-DOM-003, PDF:I.26-I.29/II.2.6/
-- II.3.4-II.3.5/II.5.3/III.8). Additive only: no existing column or
-- constraint on payments.payments or payments.payment_allocations is
-- altered or dropped.
--
-- A RefundIntent is deliberately NOT the actual refund ledger
-- (payment_reversals, V0-DOM-003's own model) — that table, the real
-- Payment Approved/PartiallyRefunded/Refunded transition and the net-paid
-- mutation are all V13-ALC-004's own scope (this task's Handoff), which
-- has not landed yet (out of this task's Owned surface). This table only
-- records that a refund was REQUESTED against a specific
-- PaymentAllocation, its eligibility having been checked against that
-- allocation's own amount (the "cumulative eligibility snapshot" is
-- purely the sum of this table's own Pending rows for that allocation —
-- there is no payment_reversals table yet to also net against).
CREATE TABLE IF NOT EXISTS payments.refund_intents (
    refund_intent_id        UUID          NOT NULL,
    payment_id               UUID          NOT NULL,
    payment_allocation_id    UUID          NOT NULL,
    requested_amount         NUMERIC(18,2) NOT NULL CHECK (requested_amount > 0),
    status                   TEXT          NOT NULL CHECK (status IN ('Pending', 'Rejected')),
    idempotency_key          TEXT          NOT NULL,
    requested_by             UUID          NULL,
    requested_at             TIMESTAMPTZ   NOT NULL,
    rejected_at              TIMESTAMPTZ   NULL,
    rejection_reason         TEXT          NULL,
    row_version               BIGINT        NOT NULL DEFAULT 1,
    PRIMARY KEY (refund_intent_id),
    CONSTRAINT uq_refund_intents_idempotency_key UNIQUE (idempotency_key),
    CONSTRAINT fk_refund_intents_payment
        FOREIGN KEY (payment_id) REFERENCES payments.payments (payment_id),
    CONSTRAINT fk_refund_intents_allocation
        FOREIGN KEY (payment_allocation_id) REFERENCES payments.payment_allocations (payment_allocation_id),
    -- Rejected transition invariant: a Rejected row must state why and
    -- when, a Pending row must not (mirrors the Payment aggregate's own
    -- "the fact and its detail fields move together" style checks).
    CONSTRAINT chk_refund_intents_rejected_fields CHECK (
        (status = 'Rejected' AND rejected_at IS NOT NULL AND rejection_reason IS NOT NULL)
        OR (status = 'Pending' AND rejected_at IS NULL AND rejection_reason IS NULL)
    )
);

CREATE INDEX IF NOT EXISTS ix_refund_intents_allocation ON payments.refund_intents (payment_allocation_id);
CREATE INDEX IF NOT EXISTS ix_refund_intents_payment ON payments.refund_intents (payment_id);
