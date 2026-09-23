-- V13-PAY-004: card settlement orchestration durable state (PDF:I.26-I.29/
-- I.49/II.2.6/II.2.16/II.5.3-II.5.4/III.8/III.19). Additive only: no
-- existing column or constraint on payments.payments or
-- payments.payment_allocations is altered or dropped.
--
-- One row per BankCard settlement attempt handed to this orchestrator by a
-- future terminal integration (V13-HUG-001). idempotency_key is the
-- correlation key the orchestrator's own caller supplies (namespaced and
-- locked on by the application, same pattern as
-- payments.payment_allocations.idempotency_key); provider_correlation_id is
-- the terminal's own transaction reference. Together they let a retry after
-- a crash (before the caller ever saw a response) replay the exact same
-- outcome instead of re-processing, and let a genuinely different terminal
-- reference reusing the same idempotency_key be rejected as a mismatch
-- instead of silently overwritten.
CREATE TABLE IF NOT EXISTS payments.card_settlement_attempts (
    card_settlement_attempt_id  UUID          NOT NULL,
    idempotency_key             TEXT          NOT NULL,
    provider_correlation_id     TEXT          NOT NULL,
    payment_id                  UUID          NOT NULL,
    outcome                     TEXT          NOT NULL
                                 CHECK (outcome IN ('Approved', 'Declined', 'RequiresReconciliation')),
    approved_amount             NUMERIC(18,2) NULL CHECK (approved_amount IS NULL OR approved_amount > 0),
    allocation_id               UUID          NULL,
    reason                      TEXT          NULL,
    fiscal_handoff_queued       BOOLEAN       NOT NULL DEFAULT false,
    created_at                  TIMESTAMPTZ   NOT NULL,
    PRIMARY KEY (card_settlement_attempt_id),
    CONSTRAINT uq_card_settlement_attempts_idempotency_key UNIQUE (idempotency_key),
    CONSTRAINT fk_card_settlement_attempts_payment
        FOREIGN KEY (payment_id) REFERENCES payments.payments (payment_id),
    CONSTRAINT fk_card_settlement_attempts_allocation
        FOREIGN KEY (allocation_id) REFERENCES payments.payment_allocations (payment_allocation_id),
    -- Approved is the only outcome that ever allocates or queues a fiscal
    -- handoff; Declined/RequiresReconciliation never do (V13-PAY-004 Out of
    -- scope: no refund, no reconciliation case persistence here).
    CONSTRAINT ck_card_settlement_attempts_approved_fields
        CHECK (
            (outcome = 'Approved' AND approved_amount IS NOT NULL AND allocation_id IS NOT NULL AND fiscal_handoff_queued)
            OR
            (outcome <> 'Approved' AND approved_amount IS NULL AND allocation_id IS NULL AND NOT fiscal_handoff_queued)
        )
);

CREATE INDEX IF NOT EXISTS ix_card_settlement_attempts_payment ON payments.card_settlement_attempts (payment_id);
