-- V12-REC-001: every retry a manager asks for on an online order reconciliation case, written in
-- the same transaction as the retry's own effect (a requeued provider update, a reprocessed
-- provider event), so what was retried, by whom and with which result is never lost even when
-- the retry itself found nothing left to do.
CREATE SCHEMA IF NOT EXISTS reconciliation;

CREATE TABLE IF NOT EXISTS reconciliation.online_order_retry_attempts (
    attempt_id    UUID          NOT NULL,
    case_id       UUID          NOT NULL REFERENCES reconciliation.cases (case_id) ON DELETE RESTRICT,
    action        VARCHAR(40)   NOT NULL,
    outcome       VARCHAR(20)   NOT NULL CHECK (outcome IN ('Requeued', 'NothingToRetry')),
    source_ref    TEXT          NOT NULL,
    performed_by  UUID          NOT NULL,
    performed_at  TIMESTAMPTZ   NOT NULL DEFAULT now(),
    PRIMARY KEY (attempt_id)
);

CREATE INDEX IF NOT EXISTS ix_online_order_retry_attempts_case
    ON reconciliation.online_order_retry_attempts (case_id, performed_at);
